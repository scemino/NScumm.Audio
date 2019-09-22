//
//  BamPlayer.cs
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
    /// Bob's Adlib Music Player, by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class BamPlayer : IMusicPlayer
    {
        private static readonly ushort[] freq= {172,182,193,205,217,230,243,258,274,
        290,307,326,345,365,387,410,435,460,489,517,547,580,614,651,1369,1389,1411,
        1434,1459,1484,1513,1541,1571,1604,1638,1675,2393,2413,2435,2458,2483,2508,
        2537,2565,2595,2628,2662,2699,3417,3437,3459,3482,3507,3532,3561,3589,3619,
        3652,3686,3723,4441,4461,4483,4506,4531,4556,4585,4613,4643,4676,4710,4747,
        5465,5485,5507,5530,5555,5580,5609,5637,5667,5700,5734,5771,6489,6509,6531,
        6554,6579,6604,6633,6661,6691,6724,6758,6795,7513,7533,7555,7578,7603,7628,
        7657,7685,7715,7748,7782,7819,7858,7898,7942,7988,8037,8089,8143,8191,8191,
        8191,8191,8191,8191,8191,8191,8191,8191,8191,8191};

        private byte[] song; byte del;
        private long pos, size, gosub;
        private bool songend, chorus;

        struct Label
        {
            public long target;
            public bool defined;
            public byte count;
        }

        private Label[] label = new Label[16];

        public IOpl Opl { get; }

        public float RefreshRate => 25.0f;

        public BamPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                size = fs.Length - 4;
                var id = new string(br.ReadChars(4));
                if (!string.Equals(id, "CBMF", StringComparison.OrdinalIgnoreCase)) return false;

                song = br.ReadBytes((int)size);

                Rewind(0);
                return true;
            }
        }

        public bool Update()
        {
            if (del != 0)
            {
                del--;
                return !songend;
            }

            if (pos >= size)
            { // EOF detection
                pos = 0;
                songend = true;
            }

            while (song[pos] < 128)
            {
                var cmd = (byte)(song[pos] & 240);
                var c = (byte)(song[pos] & 15);
                switch (cmd)
                {
                    case 0:   // stop song
                        pos = 0;
                        songend = true;
                        break;
                    case 16:  // start note
                        if (c < 9)
                        {
                            Opl.WriteReg(0xa0 + c, freq[song[++pos]] & 255);
                            Opl.WriteReg(0xb0 + c, (freq[song[pos]] >> 8) + 32);
                        }
                        else
                            pos++;
                        pos++;
                        break;
                    case 32:  // stop note
                        if (c < 9)
                            Opl.WriteReg(0xb0 + c, 0);
                        pos++;
                        break;
                    case 48:  // define instrument
                        if (c < 9)
                        {
                            Opl.WriteReg(0x20 + OplHelper.op_table[c], song[pos + 1]);
                            Opl.WriteReg(0x23 + OplHelper.op_table[c], song[pos + 2]);
                            Opl.WriteReg(0x40 + OplHelper.op_table[c], song[pos + 3]);
                            Opl.WriteReg(0x43 + OplHelper.op_table[c], song[pos + 4]);
                            Opl.WriteReg(0x60 + OplHelper.op_table[c], song[pos + 5]);
                            Opl.WriteReg(0x63 + OplHelper.op_table[c], song[pos + 6]);
                            Opl.WriteReg(0x80 + OplHelper.op_table[c], song[pos + 7]);
                            Opl.WriteReg(0x83 + OplHelper.op_table[c], song[pos + 8]);
                            Opl.WriteReg(0xe0 + OplHelper.op_table[c], song[pos + 9]);
                            Opl.WriteReg(0xe3 + OplHelper.op_table[c], song[pos + 10]);
                            Opl.WriteReg(0xc0 + c, song[pos + 11]);
                        }
                        pos += 12;
                        break;
                    case 80:  // set label
                        label[c].target = ++pos;
                        label[c].defined = true;
                        break;
                    case 96:  // jump
                        if (label[c].defined)
                            switch (song[pos + 1])
                            {
                                case 254: // infinite loop
                                // fall through...
                                case 255: // chorus
                                // fall through...
                                case 0:   // end of loop
                                    if (song[pos + 1] == 254 && label[c].defined)
                                    {
                                        pos = label[c].target;
                                        songend = true;
                                        break;
                                    }
                                    if (song[pos + 1] == 255 && !chorus && label[c].defined)
                                    {
                                        chorus = true;
                                        gosub = pos + 2;
                                        pos = label[c].target;
                                        break;
                                    }
                                    pos += 2;
                                    break;
                                default:  // finite loop
                                    if (label[c].count == 0)
                                    { // loop elapsed
                                        label[c].count = 255;
                                        pos += 2;
                                        break;
                                    }
                                    if (label[c].count < 255) // loop defined
                                        label[c].count--;
                                    else            // loop undefined
                                        label[c].count = (byte)(song[pos + 1] - 1);
                                    pos = label[c].target;
                                    break;
                            }
                        break;
                    case 112: // end of chorus
                        if (chorus)
                        {
                            pos = gosub;
                            chorus = false;
                        }
                        else
                            pos++;
                        break;
                    default:  // reserved command (skip)
                        pos++;
                        break;
                }
            }
            if (song[pos] >= 128)
            {   // wait
                del = (byte)(song[pos] - 127);
                pos++;
            }
            return !songend;
        }

        private void Rewind(int subsong)
        {
            pos = 0; songend = false; del = 0; gosub = 0; chorus = false;
            for (var i = 0; i < 16; i++)
            {
                label[i].defined = false;
                label[i].target = 0;
                label[i].count = 255;  // 255 = undefined
            }
            label[0].defined = true;
            Opl.WriteReg(1, 32);
        }
    }
}
