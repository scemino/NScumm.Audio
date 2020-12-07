//
//  ImfPlayer.cs
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
using System.Text;
using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players
{
    /// <summary>
    /// IMF Player by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    public class ImfPlayer : IMusicPlayer
    {
        private string track_name, game_name, author_name, remarks;
        private long _size;
        private Sdata[] _data;
        private string _footer;
        private float _rate = 700.0f;
        private int _pos;
        private bool _songend;
        private ushort _del;

        public IOpl Opl { get; }

        public float RefreshRate { get; set; }

        struct Sdata
        {
            public byte reg, val;
            public ushort time;
        }

        public ImfPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".imf", System.StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(Path.GetExtension(path), ".wlf", System.StringComparison.OrdinalIgnoreCase))
            {
                // It's no IMF file at all
                return false;
            }

            _rate = GetRate(path);

            using (var fs = File.OpenRead(path))
            {
                return Load(fs);
            }
        }

        public bool Load(Stream stream)
        {
            var br = new BinaryReader(stream);
            long fsize, flsize, mfsize = 0;
            uint i;

            // file validation section
            {
                var header = new string(br.ReadChars(5));
                var version = br.ReadByte();

                if (header != "ADLIB" || version != 1)
                {
                    stream.Seek(0, SeekOrigin.Begin); // It's a normal IMF file
                }
                else
                {
                    // It's a IMF file with header
                    track_name = ReadString(br);
                    game_name = ReadString(br);
                    br.ReadByte();
                    mfsize = stream.Position + 2;
                }
            }

            // load section
            if (mfsize > 0)
                fsize = br.ReadInt32();
            else
                fsize = br.ReadInt16();
            flsize = stream.Length;
            if (fsize == 0)
            {       // footerless file (raw music data)
                if (mfsize != 0)
                    stream.Seek(-4, SeekOrigin.Current);
                else
                    stream.Seek(-2, SeekOrigin.Current);
                _size = (flsize - mfsize) / 4;
            }
            else        // file has got a footer
                _size = fsize / 4;

            _data = new Sdata[_size];
            for (i = 0; i < _size; i++)
            {
                _data[i].reg = br.ReadByte(); _data[i].val = br.ReadByte();
                _data[i].time = br.ReadUInt16();
            }

            // read footer, if any
            if (fsize != 0 && (fsize < flsize - 2 - mfsize))
            {
                if (br.ReadByte() == 0x1a)
                {
                    // Adam Nielsen's footer format
                    track_name = ReadString(br);
                    author_name = ReadString(br);
                    remarks = ReadString(br);
                }
                else
                {
                    // Generic footer
                    long footerlen = flsize - fsize - 2 - mfsize;

                    _footer = ReadString(br, footerlen);
                }
            }

            _pos = 0; _del = 0; RefreshRate = _rate; _songend = false;
            Opl.WriteReg(1, 32);    // go to OPL2 mode

            return true;
        }

        public bool Update()
        {
            do
            {
                Opl.WriteReg(_data[_pos].reg, _data[_pos].val);
                _del = _data[_pos].time;
                _pos++;
            } while (_del == 0 && _pos < _size);

            if (_pos >= _size)
            {
                _pos = 0;
                _songend = true;
            }
            else RefreshRate = _rate / _del;

            return !_songend;
        }

        private static string ReadString(BinaryReader br, long length = long.MaxValue)
        {
            var text = new StringBuilder();
            char c;
            long i = 0;
            while ((c = br.ReadChar()) != 0 && i++ < length)
            {
                text.Append(c);
            }
            return text.ToString();
        }

        private static float GetRate(string path)
        {
            // Otherwise the database is either unavailable, or there's no entry for this file
            if (string.Equals(Path.GetExtension(path), ".imf", System.StringComparison.OrdinalIgnoreCase)) return 560.0f;
            if (string.Equals(Path.GetExtension(path), ".wlf", System.StringComparison.OrdinalIgnoreCase)) return 700.0f;
            return 700.0f; // default speed for unknown files that aren't .IMF or .WLF
        }
    }
}
