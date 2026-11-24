using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

internal enum DiskKind { Unknown, Hdd, Ssd, Nvme }

internal sealed class PcHardware
{
    internal string[] Processors = new string[0];
    internal string[] Graphics = new string[0];
    internal int MemoryGb;
    internal int RamProfile = -1;
    internal DriveProfile[] Drives = new DriveProfile[0];
    internal string SystemDrive;

    internal sealed class DriveProfile
    {
        internal string Letter;
        internal string Label;
        internal DiskKind Kind;
        internal string Media
        {
            get
            {
                return Kind == DiskKind.Nvme ? "NVMe SSD" : Kind == DiskKind.Ssd ? "SSD" :
                    Kind == DiskKind.Hdd ? "HDD" : "Unknown drive type";
            }
        }
        internal long FreeBytes;
        internal long FreeGb { get { return FreeBytes / 1000000000; } }
        internal string FreeSpace
        {
            get
            {
                if (FreeBytes == 0) return "0 GB";
                if (FreeBytes < 1000000) return "< 1 MB";
                if (FreeGb == 0) return (FreeBytes / 1000000) + " MB";
                return (FreeBytes / 1000000000.0).ToString("0.#") + " GB";
            }
        }

        public override string ToString()
        {
            return Letter + (String.IsNullOrEmpty(Label) ? "" : "  " + Label);
        }
    }

    internal static PcHardware Read()
    {
        var pc = new PcHardware();
        pc.Processors = Names(@"root\cimv2", "SELECT Name FROM Win32_Processor", "Name");
        pc.Graphics = Names(@"root\cimv2", "SELECT Name FROM Win32_VideoController", "Name");
        long memory = 0;
        var memoryProfiles = new HashSet<int>();
        ReadRows(@"root\cimv2", "SELECT Capacity, SMBIOSMemoryType, ConfiguredClockSpeed FROM Win32_PhysicalMemory", row =>
        {
            memory += Convert.ToInt64(row["Capacity"]);
            int type = Convert.ToInt32(row["SMBIOSMemoryType"]);
            int speed = Convert.ToInt32(row["ConfiguredClockSpeed"]);
            memoryProfiles.Add(speed == 0 ? -1 : type == 34 ? (speed >= 6000 ? 0 : 1) :
                type == 26 ? (speed >= 3200 ? 2 : 3) : -1);
        });
        pc.MemoryGb = (int)Math.Round(memory / 1073741824.0);
        if (memoryProfiles.Count == 1) pc.RamProfile = memoryProfiles.First();

        pc.SystemDrive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
        var physicalDisks = new List<PhysicalDisk>();
        ReadRows(@"root\Microsoft\Windows\Storage",
            "SELECT FriendlyName, SerialNumber, BusType, MediaType FROM MSFT_PhysicalDisk", disk =>
            {
                physicalDisks.Add(new PhysicalDisk
                {
                    Name = Convert.ToString(disk["FriendlyName"]).Trim(),
                    Serial = Convert.ToString(disk["SerialNumber"]).Trim(),
                    Media = Convert.ToInt32(disk["MediaType"]),
                    Bus = Convert.ToInt32(disk["BusType"])
                });
            });
        var drives = new List<DriveProfile>();
        foreach (var drive in DriveInfo.GetDrives().Where(item => item.DriveType == DriveType.Fixed))
        {
            try
            {
                if (drive.IsReady) drives.Add(DescribeDrive(drive, physicalDisks));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        pc.Drives = drives.ToArray();
        return pc;
    }

    sealed class PhysicalDisk
    {
        internal string Name;
        internal string Serial;
        internal int Media;
        internal int Bus;
    }

    static DriveProfile DescribeDrive(DriveInfo drive, List<PhysicalDisk> physicalDisks)
    {
        var result = new DriveProfile { Letter = drive.Name.TrimEnd('\\'),
            Label = drive.VolumeLabel, FreeBytes = drive.AvailableFreeSpace };
        var mappings = new List<PhysicalDisk>();
        if (physicalDisks.Count > 0)
        {
            string query = "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + result.Letter +
                "'} WHERE AssocClass=Win32_LogicalDiskToPartition";
            ReadRows(@"root\cimv2", query, partition =>
            {
                string diskQuery = "ASSOCIATORS OF {" + partition.Path.RelativePath +
                    "} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                ReadRows(@"root\cimv2", diskQuery, disk =>
                {
                    string uid = Convert.ToString(disk["SerialNumber"]).Trim();
                    string model = Convert.ToString(disk["Model"]).Trim();
                    mappings.AddRange(physicalDisks.Where(item => MatchesDisk(uid, item.Serial, model, item.Name)));
                });
            });
        }

        var matches = mappings.Distinct().ToArray();
        if (matches.Length == 1)
        {
            var disk = matches[0];
            if (disk.Media == 3) result.Kind = DiskKind.Hdd;
            else if (disk.Bus == 17) result.Kind = DiskKind.Nvme;
            else if (disk.Media == 4) result.Kind = DiskKind.Ssd;
        }
        return result;
    }

    static bool MatchesDisk(string uid, string serial, string model, string name)
    {
        return (!String.IsNullOrEmpty(uid) && serial.Length > 0 &&
            String.Equals(uid, serial, StringComparison.OrdinalIgnoreCase)) ||
            (!String.IsNullOrEmpty(model) && name.Length > 0 &&
             String.Equals(model, name, StringComparison.OrdinalIgnoreCase));
    }

    static string[] Names(string scope, string query, string property)
    {
        var names = new List<string>();
        ReadRows(scope, query, row =>
        {
            string name = Convert.ToString(row[property]).Trim();
            if (name.Length != 0) names.Add(name);
        });
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    static void ReadRows(string scope, string query, Action<ManagementObject> read)
    {
        try
        {
            using (var search = new ManagementObjectSearcher(scope, query,
                new EnumerationOptions { Timeout = TimeSpan.FromSeconds(5) }))
            using (var results = search.Get())
                foreach (ManagementObject row in results)
                    using (row) read(row);
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    internal static string NormalizeModel(string name)
    {
        name = Regex.Replace(name.ToLowerInvariant(), @"\((?:r|tm)\)", "");
        name = Regex.Replace(name, @"\b(?:nvidia|geforce|amd|intel|core|cpu|processor)\b", "");
        return Regex.Replace(name, @"[^a-z0-9]+", " ").Trim();
    }
}
