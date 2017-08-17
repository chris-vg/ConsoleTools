using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using PlayStation2Tools.Model;

namespace PlayStation2Tools.Resources
{
    internal class HdlDump
    {
        public string Target { get; private set; }
        public string Path { get; private set; }

        public HdlDump()
        {
            GetHdlDumpPath();
            ValidateHdlDumpPath();
        }

        private void GetHdlDumpPath()
        {
            Path = Environment.GetEnvironmentVariable("HDLDump", EnvironmentVariableTarget.User);

            if (Path == null)
            {
                throw new ApplicationException("$env:HDLDump not set.  Use Set-HDLDumpPath to set the environment variable.");
            }
        }

        private void ValidateHdlDumpPath()
        {
            FileVersionInfo hdlVersionInfo;
            try
            {
                hdlVersionInfo = FileVersionInfo.GetVersionInfo(Path);
            }
            catch
            {
                throw new ArgumentException($"Path \"{Path}\" is not a file.");
            }

            if (hdlVersionInfo.ProductName != "hdl_dump")
                throw new ArgumentException($"Path \"{Path}\" is not HDL_Dump.");
        }

        public string GetHdlDumpVersion()
        {
            return $"{FileVersionInfo.GetVersionInfo(Path).ProductVersion.Replace(", ", ".")}";
        }

        public void SetTarget(Target target, string ipAddress = null)
        {
            switch (target)
            {
                    case PlayStation2Tools.Target.HardDrive:
                        SetPs2Hdd();
                        break;
                    case PlayStation2Tools.Target.Network:
                        SetIpAddress(ipAddress);
                        break;
            }
        }

        private void SetIpAddress(string ipAddress)
        {
            if (ipAddress == null) throw new ApplicationException("IPAddress is null.");
            if (IPAddress.TryParse(ipAddress, out IPAddress ps2IpAddress))
            {
                Target = ps2IpAddress.ToString();
            }
            else
            {
                throw new ApplicationException($"IPAddress \"{ps2IpAddress}\" is not valid.");
            }
        }

        private void SetPs2Hdd()
        {
            var p = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = "query",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            p.Start();

            const string pattern = @"\s+(\w+\d+:).*";

            do
            {
                var output = p.StandardOutput.ReadLine();
                if (output == null || !output.Contains("formatted Playstation 2 HDD")) continue;
                var matches = Regex.Matches(output, pattern);
                Target = matches[0].Groups[1].Value;
            } while (!p.StandardOutput.EndOfStream);

            if (Target == null)
            {
                throw new ApplicationException("Unable to find a PlayStation 2 harddrive.");
            }

            p.WaitForExit();
            while (!p.HasExited)
            {
                // wait
            }
        }

        public CdvdInfo GetCdvdInfo(FileSystemInfo file)
        {
            var p = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = $"cdvd_info \"{file.FullName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            p.Start();

            var output = p.StandardOutput.ReadToEnd();

            p.WaitForExit();
            while (!p.HasExited && p.Responding)
            {
                Thread.Sleep(100);
            }

            const string pattern = @"^""([A-Z0-9_.]+){1}""\s""(.*){1}""\s([a-z ]+){0,1}\s*(?:([0-9]+)KB)";

            var matches = Regex.Matches(output, pattern);
            var cdvdInfo = new CdvdInfo
            {
                Signature = matches[0].Groups[1].Value,
                VolumeLabel = matches[0].Groups[2].Value,
                DataSize = Convert.ToInt32(matches[0].Groups.Count == 4
                    ? matches[0].Groups[3].Value
                    : matches[0].Groups[4].Value)
            };

            return cdvdInfo;
        }

        public List<HdlTocItem> GetHdlToc()
        {
            var p = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = $"hdl_toc {Target}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            p.Start();

            const string pattern = @"^(DVD|CD){1}(?:\s*([0-9]+)KB){1}(?:\s*((?:(?:\+[+0-9]+)|(?:0x[0-9A-Fa-f][0-9A-Fa-f]))))?(?:\s*(\*(?:u|m)[0-4]))?(?:\s*([A-Z0-9_.]+)){1}(?:\s*(.*))$";

            var hdlToc = new List<HdlTocItem>();

            do
            {
                var output = p.StandardOutput.ReadLine();
                if (output == null) continue;

                var match = Regex.Match(output, pattern);
                if (!match.Success) continue;

                var hdlTocItem = new HdlTocItem();

                if (match.Groups[1].Success) hdlTocItem.Type = match.Groups[1].Value;
                if (match.Groups[2].Success) hdlTocItem.Size = Convert.ToInt32(match.Groups[2].Value);
                if (match.Groups[3].Success) hdlTocItem.Flags = match.Groups[3].Value;
                if (match.Groups[4].Success) hdlTocItem.Dma = match.Groups[4].Value;
                if (match.Groups[5].Success) hdlTocItem.Startup = match.Groups[5].Value;
                if (match.Groups[6].Success) hdlTocItem.Name = match.Groups[6].Value;

                hdlToc.Add(hdlTocItem);

            } while (!p.StandardOutput.EndOfStream);

            return hdlToc;
        }

        public string GetInstallGameArgs(Ps2Game ps2Game)
        {
            return $"{(ps2Game.CdvdInfo.DataSize <= 800000 ? "inject_cd" : "inject_dvd")} {Target} \"{ps2Game.InstallTitle}\" \"{ps2Game.File.FullName}\" {ps2Game.CdvdInfo.Signature} *u4";
        }
    }
}