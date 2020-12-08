//
//  MkjPlayer.cs
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
    /// MKJamz Player, by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class MkjPlayer : IMusicPlayer
    {
        private short maxchannel, maxnotes;
        private short[] songbuf;
        private bool songend;

        struct Channel
        {
            public short defined, songptr, octave, waveform, pstat, speed, delay;
        }
        private Channel[] channel = new Channel[9];

        public IOpl Opl { get; }

        public float RefreshRate => 100.0f;

        public MkjPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                return Load(fs);
            }
        }

        public bool Load(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                // file validation
                var id = new string(br.ReadChars(6));
                if (!string.Equals(id, "MKJamz", StringComparison.Ordinal))
                    return false;
                var ver = br.ReadSingle();
                if (ver > 1.12) return false;

                // load
                maxchannel = br.ReadInt16();
                Opl.WriteReg(1, 32);
                short[] inst = new short[8];
                for (var i = 0; i < maxchannel; i++)
                {
                    for (var j = 0; j < 8; j++) inst[j] = br.ReadInt16();
                    Opl.WriteReg((byte)(0x20 + OplHelper.op_table[i]), inst[4]);
                    Opl.WriteReg((byte)(0x23 + OplHelper.op_table[i]), inst[0]);
                    Opl.WriteReg((byte)(0x40 + OplHelper.op_table[i]), inst[5]);
                    Opl.WriteReg((byte)(0x43 + OplHelper.op_table[i]), inst[1]);
                    Opl.WriteReg((byte)(0x60 + OplHelper.op_table[i]), inst[6]);
                    Opl.WriteReg((byte)(0x63 + OplHelper.op_table[i]), inst[2]);
                    Opl.WriteReg((byte)(0x80 + OplHelper.op_table[i]), inst[7]);
                    Opl.WriteReg((byte)(0x83 + OplHelper.op_table[i]), inst[3]);
                }
                maxnotes = br.ReadInt16();
                songbuf = new short[(maxchannel + 1) * maxnotes];
                for (var i = 0; i < maxchannel; i++) channel[i].defined = br.ReadInt16();
                for (var i = 0; i < (maxchannel + 1) * maxnotes; i++)
                    songbuf[i] = br.ReadInt16();

                // AdPlug_LogWrite("CmkjPlayer::load(\"%s\"): loaded file ver %.2f, %d channels,"

                // " %d notes/channel.\n", filename.c_str(), ver, maxchannel,
                // maxnotes);
                Rewind(0);
                return true;
            }
        }

        public bool Update()
        {
            for (var c = 0; c < maxchannel; c++)
            {
                if (channel[c].defined == 0)  // skip if channel is disabled
                    continue;

                if (channel[c].pstat != 0)
                {
                    channel[c].pstat--;
                    continue;
                }

                Opl.WriteReg(0xb0 + c, 0);  // key off
                do
                {
                    // assert(channel[c].songptr < (maxchannel + 1) * maxnotes);
                    var note = songbuf[channel[c].songptr];
                    if (channel[c].songptr - c > maxchannel)
                        if (note != 0 && note < 250)
                            channel[c].pstat = channel[c].speed;
                    switch (note)
                    {
                        // normal notes
                        case 68: Opl.WriteReg(0xa0 + c, 0x81); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 69: Opl.WriteReg(0xa0 + c, 0xb0); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 70: Opl.WriteReg(0xa0 + c, 0xca); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 71: Opl.WriteReg(0xa0 + c, 0x2); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 65: Opl.WriteReg(0xa0 + c, 0x41); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 66: Opl.WriteReg(0xa0 + c, 0x87); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 67: Opl.WriteReg(0xa0 + c, 0xae); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 17: Opl.WriteReg(0xa0 + c, 0x6b); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 18: Opl.WriteReg(0xa0 + c, 0x98); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 20: Opl.WriteReg(0xa0 + c, 0xe5); Opl.WriteReg(0xb0 + c, 0x21 + 4 * channel[c].octave); break;
                        case 21: Opl.WriteReg(0xa0 + c, 0x20); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 15: Opl.WriteReg(0xa0 + c, 0x63); Opl.WriteReg(0xb0 + c, 0x22 + 4 * channel[c].octave); break;
                        case 255: // delay
                            channel[c].songptr += maxchannel;
                            channel[c].pstat = songbuf[channel[c].songptr];
                            break;
                        case 254: // set octave
                            channel[c].songptr += maxchannel;
                            channel[c].octave = songbuf[channel[c].songptr];
                            break;
                        case 253: // set speed
                            channel[c].songptr += maxchannel;
                            channel[c].speed = songbuf[channel[c].songptr];
                            break;
                        case 252: // set waveform
                            channel[c].songptr += maxchannel;
                            channel[c].waveform = (short)(songbuf[channel[c].songptr] - 300);
                            if (c > 2)
                                Opl.WriteReg(0xe0 + c + (c + 6), channel[c].waveform);
                            else
                                Opl.WriteReg(0xe0 + c, channel[c].waveform);
                            break;
                        case 251: // song end
                            for (var i = 0; i < maxchannel; i++) channel[i].songptr = (short)i;
                            songend = true;
                            return false;
                    }

                    if (channel[c].songptr - c < maxnotes)
                        channel[c].songptr += maxchannel;
                    else
                        channel[c].songptr = (short)c;
                } while (channel[c].pstat == 0);
            }

            return !songend;
        }

        private void Rewind(int subsong)
        {
            for (var i = 0; i < maxchannel; i++)
            {
                channel[i].pstat = 0;
                channel[i].speed = 0;
                channel[i].waveform = 0;
                channel[i].songptr = (short)i;
                channel[i].octave = 4;
            }

            songend = false;
        }
    }
}
