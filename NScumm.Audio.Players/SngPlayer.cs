//
//  SngPlayer.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2019 
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players
{
    /// <summary>
    /// SNG Player by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class SngPlayer : IMusicPlayer
    {
        struct Header
        {

            public string id;
            public ushort length, start, loop;
            public byte delay;
            public bool compressed;
        }

        struct Sdata
        {
            public byte reg, val;
        }

        private Header header;
        private byte del;
        private ushort pos;
        private bool songend;
        private Sdata[] data;

        public IOpl Opl { get; }

        public float RefreshRate => 70.0f;

        public SngPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var br = new BinaryReader(fs);

                // load header
                header.id = new string(br.ReadChars(4));
                header.length = br.ReadUInt16(); header.start = br.ReadUInt16();
                header.loop = br.ReadUInt16(); header.delay = br.ReadByte();
                header.compressed = br.ReadByte() != 0;

                // file validation section
                if (!string.Equals(header.id, "ObsM", System.StringComparison.OrdinalIgnoreCase)) return false;

                // load section
                header.length /= 2; header.start /= 2; header.loop /= 2;
                data = new Sdata[header.length];
                for (var i = 0; i < header.length && fs.Position < fs.Length-2; i++)
                {
                    data[i].val = br.ReadByte();
                    data[i].reg = br.ReadByte();
                }

                pos = header.start; del = header.delay; songend = false;
                Opl.WriteReg(1, 32);	// go to OPL2 mode
                return true;
            }
        }

        public bool Update()
        {
            if (header.compressed && del != 0)
            {
                del--;
                return !songend;
            }

            while (data[pos].reg != 0)
            {
                Opl.WriteReg(data[pos].reg, data[pos].val);
                pos++;
                if (pos >= header.length)
                {
                    songend = true;
                    pos = header.loop;
                }
            }

            if (!header.compressed)
                Opl.WriteReg(data[pos].reg, data[pos].val);

            if (data[pos].val != 0) del = (byte)(data[pos].val - 1); pos++;
            if (pos >= header.length) { songend = true; pos = header.loop; }
            return !songend;
        }
    }
}
