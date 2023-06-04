using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Process _process = new Process();

            string commandPath = WhereSeach("Khoj");
            string arguments = $"--no-gui {args}";

            _process = new Process();
            _process.StartInfo = new ProcessStartInfo(commandPath);
            _process.StartInfo.Arguments = arguments;
            _process.Start();
        }

        private static string WhereSeach(string fileName)
        {
            var paths = new[] { Environment.CurrentDirectory }.Concat(Environment.GetEnvironmentVariable("PATH").Split(';'));
            var extensions = new[] { string.Empty }.Concat(Environment.GetEnvironmentVariable("PATHEXT").Split(';')).Where(e => e.StartsWith("."));
            var combinations = paths.SelectMany(x => extensions, (path, extension) => Path.Combine(path, fileName + extension));
            return combinations.FirstOrDefault(File.Exists);
        }
    }
}
