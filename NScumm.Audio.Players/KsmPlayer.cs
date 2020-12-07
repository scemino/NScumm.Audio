//
//  KsmPlayer.cs
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
    /// KSM Player for AdPlug by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class KsmPlayer : IMusicPlayer
    {
        private static readonly uint[] adlibfreq = {
            0,
            2390,2411,2434,2456,2480,2506,2533,2562,2592,2625,2659,2695,
            3414,3435,3458,3480,3504,3530,3557,3586,3616,3649,3683,3719,
            4438,4459,4482,4504,4528,4554,4581,4610,4640,4673,4707,4743,
            5462,5483,5506,5528,5552,5578,5605,5634,5664,5697,5731,5767,
            6486,6507,6530,6552,6576,6602,6629,6658,6688,6721,6755,6791,
            7510
        };

        private long count, countstop;
        private long[] chanage = new long[18];
        private int[] note;
        private ushort numnotes;
        private int nownote, numchans, drumstat;
        private byte[] trinst = new byte[16], trquant = new byte[16], trchan = new byte[16], trvol = new byte[16];
        private byte[,] inst = new byte[256, 11];
        private byte[] databuf = new byte[2048], chanfreq = new byte[18], chantrack = new byte[18];
        private string[] instname = new string[256];

        private bool songend;

        public IOpl Opl { get; }

        public float RefreshRate => 240.0f;

        public KsmPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".ksm", StringComparison.OrdinalIgnoreCase))
            {
                AdPlug_LogWrite($"CksmPlayer::load(,\"{path}\"): File doesn't have '.ksm' extension! Rejected!\n");
                return false;
            }
            using (var fs = File.OpenRead(path))
            {
                return Load(fs);
            }
        }

        public bool Load(Stream stream)
        {
            var br = new BinaryReader(stream);
            AdPlug_LogWrite($"*** CksmPlayer::load ***\n");

            // Load instruments from 'insts.dat'
            var fs = stream as FileStream;
            if (fs == null)
            {
                AdPlug_LogWrite("Couldn't open instruments for Ksm from a pure stream! Aborting!\n");
                return false;
            }
            var fn = Path.Combine(Path.GetDirectoryName(fs.Name), "insts.dat");
            if (!File.Exists(fn))
            {
                AdPlug_LogWrite("Couldn't open instruments file! Aborting!\n");
                AdPlug_LogWrite("--- CksmPlayer::load ---\n");
                return false;
            }
            AdPlug_LogWrite($"Instruments file: \"{fn}\"\n");
            loadinsts(fn);

            for (var i = 0; i < 16; i++) trinst[i] = br.ReadByte();
            for (var i = 0; i < 16; i++) trquant[i] = br.ReadByte();
            for (var i = 0; i < 16; i++) trchan[i] = br.ReadByte();
            stream.Seek(16, SeekOrigin.Current);
            for (var i = 0; i < 16; i++) trvol[i] = br.ReadByte();
            numnotes = br.ReadUInt16();
            note = new int[numnotes];
            for (var i = 0; i < numnotes; i++) note[i] = br.ReadInt32();

            if (trchan[11] == 0)
            {
                drumstat = 0;
                numchans = 9;
            }
            else
            {
                drumstat = 32;
                numchans = 6;
            }

            Rewind(0);
            AdPlug_LogWrite("--- CksmPlayer::load ---\n");
            return true;
        }

        public bool Update()
        {
            int quanter, chan = 0, drumnum = 0, freq, track, volevel, volval;
            int i, j, bufnum;
            long temp, templong;

            count++;
            if (count >= countstop)
            {
                bufnum = 0;
                while (count >= countstop)
                {
                    templong = note[nownote];
                    track = (int)((templong >> 8) & 15);
                    if ((templong & 192) == 0)
                    {
                        i = 0;

                        while ((i < numchans) &&
                         ((chanfreq[i] != (templong & 63)) ||
                          (chantrack[i] != ((templong >> 8) & 15))))
                            i++;
                        if (i < numchans)
                        {
                            databuf[bufnum] = 0; bufnum++;
                            databuf[bufnum] = (byte)(0xb0 + i); bufnum++;
                            databuf[bufnum] = (byte)((adlibfreq[templong & 63] >> 8) & 223); bufnum++;
                            chanfreq[i] = 0;
                            chanage[i] = 0;
                        }
                    }
                    else
                    {
                        volevel = trvol[track];
                        if ((templong & 192) == 128)
                        {
                            volevel -= 4;
                            if (volevel < 0)
                                volevel = 0;
                        }
                        if ((templong & 192) == 192)
                        {
                            volevel += 4;
                            if (volevel > 63)
                                volevel = 63;
                        }
                        if (track < 11)
                        {
                            temp = 0;
                            i = numchans;
                            for (j = 0; j < numchans; j++)
                                if ((countstop - chanage[j] >= temp) && (chantrack[j] == track))
                                {
                                    temp = countstop - chanage[j];
                                    i = j;
                                }
                            if (i < numchans)
                            {
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0xb0 + i); bufnum++;
                                databuf[bufnum] = (byte)0; bufnum++;
                                volval = (inst[trinst[track], 1] & 192) + (volevel ^ 63);
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0x40 + OplHelper.op_table[i] + 3); bufnum++;
                                databuf[bufnum] = (byte)volval; bufnum++;
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0xa0 + i); bufnum++;
                                databuf[bufnum] = (byte)(adlibfreq[templong & 63] & 255); bufnum++;
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0xb0 + i); bufnum++;
                                databuf[bufnum] = (byte)((adlibfreq[templong & 63] >> 8) | 32); bufnum++;
                                chanfreq[i] = (byte)(templong & 63);
                                chanage[i] = countstop;
                            }
                        }
                        else if ((drumstat & 32) > 0)
                        {
                            freq = (int)adlibfreq[templong & 63];
                            switch (track)
                            {
                                case 11: drumnum = 16; chan = 6; freq -= 2048; break;
                                case 12: drumnum = 8; chan = 7; freq -= 2048; break;
                                case 13: drumnum = 4; chan = 8; break;
                                case 14: drumnum = 2; chan = 8; break;
                                case 15: drumnum = 1; chan = 7; freq -= 2048; break;
                            }
                            databuf[bufnum] = (byte)0; bufnum++;
                            databuf[bufnum] = (byte)(0xa0 + chan); bufnum++;
                            databuf[bufnum] = (byte)(freq & 255); bufnum++;
                            databuf[bufnum] = (byte)0; bufnum++;
                            databuf[bufnum] = (byte)(0xb0 + chan); bufnum++;
                            databuf[bufnum] = (byte)((freq >> 8) & 223); bufnum++;
                            databuf[bufnum] = (byte)0; bufnum++;
                            databuf[bufnum] = (byte)(0xbd); bufnum++;
                            databuf[bufnum] = (byte)(drumstat & (255 - drumnum)); bufnum++;
                            drumstat |= drumnum;
                            if ((track == 11) || (track == 12) || (track == 14))
                            {
                                volval = (inst[trinst[track], 1] & 192) + (volevel ^ 63);
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0x40 + OplHelper.op_table[chan] + 3); bufnum++;
                                databuf[bufnum] = (byte)(volval); bufnum++;
                            }
                            else
                            {
                                volval = (inst[trinst[track], 6] & 192) + (volevel ^ 63);
                                databuf[bufnum] = (byte)0; bufnum++;
                                databuf[bufnum] = (byte)(0x40 + OplHelper.op_table[chan]); bufnum++;
                                databuf[bufnum] = (byte)(volval); bufnum++;
                            }
                            databuf[bufnum] = (byte)0; bufnum++;
                            databuf[bufnum] = (byte)(0xbd); bufnum++;
                            databuf[bufnum] = (byte)(drumstat); bufnum++;
                        }
                    }
                    nownote++;
                    if (nownote >= numnotes)
                    {
                        nownote = 0;
                        songend = true;
                    }
                    templong = note[nownote];
                    if (nownote == 0)
                        count = (templong >> 12) - 1;
                    quanter = (240 / trquant[(templong >> 8) & 15]);
                    countstop = (((templong >> 12) + (quanter >> 1)) / quanter) * quanter;
                }
                for (i = 0; i < bufnum; i += 3)
                    Opl.WriteReg(databuf[i + 1], databuf[i + 2]);
            }
            return !songend;
        }

        private void Rewind(int subsong)
        {
            int i, j, k;
            byte[] instbuf = new byte[11];
            int templong;

            songend = false;
            Opl.WriteReg(1, 32); Opl.WriteReg(4, 0); Opl.WriteReg(8, 0); Opl.WriteReg(0xbd, drumstat);

            if (trchan[11] == 1)
            {
                for (i = 0; i < 11; i++)
                    instbuf[i] = inst[trinst[11], i];
                instbuf[1] = (byte)(((instbuf[1] & 192) | (trvol[11]) ^ 63));
                setinst(6, instbuf[0], instbuf[1], instbuf[2], instbuf[3], instbuf[4], instbuf[5], instbuf[6], instbuf[7], instbuf[8], instbuf[9], instbuf[10]);
                for (i = 0; i < 5; i++)
                    instbuf[i] = inst[trinst[12], i];
                for (i = 5; i < 11; i++)
                    instbuf[i] = inst[trinst[15], i];
                instbuf[1] = (byte)(((instbuf[1] & 192) | (trvol[12]) ^ 63));
                instbuf[6] = (byte)(((instbuf[6] & 192) | (trvol[15]) ^ 63));
                setinst(7, instbuf[0], instbuf[1], instbuf[2], instbuf[3], instbuf[4], instbuf[5], instbuf[6], instbuf[7], instbuf[8], instbuf[9], instbuf[10]);
                for (i = 0; i < 5; i++)
                    instbuf[i] = inst[trinst[14], i];
                for (i = 5; i < 11; i++)
                    instbuf[i] = inst[trinst[13], i];
                instbuf[1] = (byte)(((instbuf[1] & 192) | (trvol[14]) ^ 63));
                instbuf[6] = (byte)(((instbuf[6] & 192) | (trvol[13]) ^ 63));
                setinst(8, instbuf[0], instbuf[1], instbuf[2], instbuf[3], instbuf[4], instbuf[5], instbuf[6], instbuf[7], instbuf[8], instbuf[9], instbuf[10]);
            }

            for (i = 0; i < numchans; i++)
            {
                chantrack[i] = 0;
                chanage[i] = 0;
            }
            j = 0;
            for (i = 0; i < 16; i++)
                if ((trchan[i] > 0) && (j < numchans))
                {
                    k = trchan[i];
                    while ((j < numchans) && (k > 0))
                    {
                        chantrack[j] = (byte)i;
                        k--;
                        j++;
                    }
                }
            for (i = 0; i < numchans; i++)
            {
                for (j = 0; j < 11; j++)
                    instbuf[j] = inst[trinst[chantrack[i]], j];
                instbuf[1] = (byte)((instbuf[1] & 192) | (63 - trvol[chantrack[i]]));
                setinst(i, instbuf[0], instbuf[1], instbuf[2], instbuf[3], instbuf[4], instbuf[5], instbuf[6], instbuf[7], instbuf[8], instbuf[9], instbuf[10]);
                chanfreq[i] = 0;
            }
            k = 0;
            templong = note[0];
            count = (templong >> 12) - 1;
            countstop = (templong >> 12) - 1;
            nownote = 0;
        }

        void setinst(int chan,
       byte v0, byte v1, byte v2,
       byte v3, byte v4, byte v5,
       byte v6, byte v7, byte v8,
       byte v9, byte v10)
        {
            Opl.WriteReg(0xa0 + chan, 0);
            Opl.WriteReg(0xb0 + chan, 0);
            Opl.WriteReg(0xc0 + chan, v10);
            var offs = OplHelper.op_table[chan];
            Opl.WriteReg(0x20 + offs, v5);
            Opl.WriteReg(0x40 + offs, v6);
            Opl.WriteReg(0x60 + offs, v7);
            Opl.WriteReg(0x80 + offs, v8);
            Opl.WriteReg(0xe0 + offs, v9);
            offs += 3;
            Opl.WriteReg(0x20 + offs, v0);
            Opl.WriteReg(0x40 + offs, v1);
            Opl.WriteReg(0x60 + offs, v2);
            Opl.WriteReg(0x80 + offs, v3);
            Opl.WriteReg(0xe0 + offs, v4);
        }

        private void loadinsts(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                for (var i = 0; i < 256; i++)
                {
                    instname[i] = new string(br.ReadChars(20));
                    for (var j = 0; j < 11; j++) inst[i, j] = br.ReadByte();
                    fs.Seek(2, SeekOrigin.Current);
                }
            }
        }

        private void AdPlug_LogWrite(string message)
        {
            Console.Error.WriteLine(message);
        }
    }
}