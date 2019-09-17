using System.IO;
using NUnit.Framework;

namespace NScumm.Audio.Players.Tests
{
    public class Tests
    {
        [Test]
        public void PlayersTest()
        {
            var directory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../../test");
            foreach (var filename in Directory.EnumerateFiles(directory))
            {
                if(Path.GetFileName(filename).StartsWith('.')) continue;
                var ext = Path.GetExtension(filename);
                if (string.Equals(ext, ".test", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(ext, ".ref", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var testFilename = Path.ChangeExtension(filename, ".test");
                using (var opl = new Testopl(testFilename))
                {
                    foreach (var player in Factory.GetPlayers(opl))
                    {
                        if (!player.Load(filename))
                            continue;

                        // Output file information
                        System.Console.WriteLine($"Testing {Path.GetFileName(filename)} with player {player}");

                        // Write whole file to disk
                        while (player.Update())
                            opl.Update(player);
                        break;
                    }
                }

                var refFilename = System.IO.Path.ChangeExtension(filename, ".ref");
                FileAssert.AreEqual(refFilename, testFilename);
            }
        }
    }

}