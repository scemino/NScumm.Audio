using System;
using System.IO;
using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players.Tests
{
    internal sealed class Testopl : IOpl, IDisposable
    {
        public Testopl(string filename)
        {
            f = new StreamWriter(File.OpenWrite(filename));
        }

        public void Dispose()
        {
            f.Dispose();
        }

        public void Update(IMusicPlayer p)
        {
            f.WriteLine($"r{p.RefreshRate:f2}");
        }

        public void WriteReg(int reg, int val)
        {
            if (reg > 255 || val > 255 || reg < 0 || val < 0)
            {
                Console.Error.WriteLine($"Warning: The player is writing data out of range! (reg = {reg:x}, val = {val}");
            }
            f.WriteLine($"{reg:x} <- {val:x}");
        }

        public void Setchip(int n)
        {
            f.WriteLine($"setchip{n}");
        }

        public void Init(int rate)
        {
            f.WriteLine("init");
        }

        void IOpl.ReadBuffer(short[] buffer, int pos, int length)
        {
            throw new NotImplementedException();
        }

        private StreamWriter f;

        bool IOpl.IsStereo => throw new NotImplementedException();
    }
}