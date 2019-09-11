//
//  DroPlayer.cs
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
    /// DOSBox Raw OPL Player by Sjoerd van der Berg harekiet@zophar.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class DroPlayer: IMusicPlayer
    {
        private const byte iCmdDelayS = 0x00;
        private const byte iCmdDelayL = 0x01;

        private byte[] _data;
        private int _pos;
        private int _delay;

        public IOpl Opl { get; }

        public float RefreshRate
        {
            get
            {
                if (_delay > 0) return 1000.0f / _delay;
                return 1000.0f;
            }
        }

        public DroPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var br = new BinaryReader(fs);
                var id = new string(br.ReadChars(8));
                if (id != "DBRAWOPL") return false;

                var version = br.ReadInt32();
                if (version != 0x10000) return false;

                var lengthInMs = br.ReadInt32();
                var length = br.ReadInt32();
                _data = new byte[length];

                // Some early .DRO files only used one byte for the hardware type, then
                // later changed to four bytes with no version number change.
                // OPL type (0 == OPL2, 1 == OPL3, 2 == Dual OPL2)
                br.ReadChar();   // Type of opl data this can contain - ignored
                int i;
                for (i = 0; i < 3; i++)
                {
                    _data[i] = br.ReadByte();
                }

                if (_data[0] == 0 || _data[1] == 0 || _data[2] == 0)
                {
                    // If we're here then this is a later (more popular) file with
                    // the full four bytes for the hardware-type.
                    i = 0; // so ignore the three bytes we just read and start again
                }

                // Read the OPL data.
                br.BaseStream.Read(_data, i, length - i);

                var tagsize = fs.Length - fs.Position;
                if (tagsize >= 3)
                {
                    // The arbitrary Tag Data section begins here.
                    if (br.ReadByte() != 0xFF ||
                        br.ReadByte() != 0xFF ||
                        br.ReadByte() != 0x1A)
                    {
                        // Tag data does not present or truncated.
                        return true;
                    }

                    // "title" is maximum 40 characters long.
                    var title = new StringBuilder();
                    char c;
                    while ((c = br.ReadChar()) != 0)
                    {
                        title.Append(c);
                    }

                    // "author" Tag marker byte is present ?
                    if (br.ReadByte() == 0x1B)
                    {
                        // "author" is maximum 40 characters long.
                        var author = new StringBuilder();
                        while ((c = br.ReadChar()) != 0)
                        {
                            author.Append(c);
                        }
                    }

                    // "desc" Tag marker byte is present..
                    if (br.ReadByte() == 0x1C)
                    {
                        // "desc" is now maximum 1023 characters long (it was 140).
                        var desc = new StringBuilder();
                        while ((c = br.ReadChar()) != 0)
                        {
                            desc.Append(c);
                        }
                    }
                }
                return true;
            }
        }

        public bool Update()
        {
            while (_pos < _data.Length)
            {
                int iIndex = _data[_pos++];
                int iValue;
                // Short delay
                if (iIndex == iCmdDelayS)
                {
                    iValue = _data[_pos++];
                    _delay = iValue + 1;
                    return true;

                    // Long delay
                }
                if (iIndex == iCmdDelayL)
                {
                    iValue = _data[_pos] | (_data[_pos + 1] << 8);
                    _pos += 2;
                    _delay = (iValue + 1);
                    return true;

                    // Bank switching
                }
                else if (iIndex == 0x02 || iIndex == 0x03)
                {
                    // TODO:?
                    //_opl.SetChip(iIndex - 0x02);

                    // Normal write
                }
                else
                {
                    if (iIndex == 0x04)
                    {
                        iIndex = _data[_pos++];
                    }
                    iValue = _data[_pos++];
                    Opl.WriteReg(iIndex, iValue);
                }
            }

            // This won't result in endless-play using Adplay, but IMHO that code belongs
            // in Adplay itself, not here.
            return _pos < _data.Length;
        }
    }
}
