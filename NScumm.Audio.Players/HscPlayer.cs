//
//  HscPlayer.cs
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
    /// HSC Player by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class HscPlayer : IMusicPlayer
    {
        struct hscnote
        {
            public byte note, effect;
        }  // note type in HSC pattern

        struct hscchan
        {
            public byte inst;     // current instrument
            public sbyte slide;     // used for manual slide-effects
            public ushort freq;   // actual replaying frequency
        }  // HSC channel data

        hscchan[] channel = new hscchan[9];     // player channel-info
        byte[][] instr;    // instrument data
        byte[] song = new byte[0x80];   // song-arrangement (MPU-401 Trakker enhanced)
        hscnote[,] patterns;    // pattern data
        byte pattpos, songpos, // various bytes & flags
          pattbreak, songend, mode6, bd, fadein;
        int speed, del;
        byte[] adl_freq = new byte[9];    // adlib frequency registers
        int mtkmode;        // flag: MPU-401 Trakker mode on/off

        public IOpl Opl { get; }

        public float RefreshRate => 18.2f; // refresh rate is fixed at 18.2Hz

        public HscPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            // file validation section
            if (!string.Equals(Path.GetExtension(path), ".hsc", StringComparison.OrdinalIgnoreCase))
                return false;

            using (var fs = File.OpenRead(path))
            {
                if (fs.Length > (59187 + 1))  // +1 is for some files that have a trailing 0x00 on the end
                    return false;
                if (fs.Length < (1587 + 1152)) // no 0x00 byte here as this is the smallest possible size
                    return false;

                var br = new BinaryReader(fs);
                int total_patterns_in_hsc = (int)((fs.Length - 1587) / 1152);

                // load section
                instr = new byte[128][];
                for (var i = 0; i < 128; i++)    // load instruments
                    instr[i] = br.ReadBytes(12);
                for (var i = 0; i < 128; i++)
                {     // correct instruments
                    instr[i][2] = (byte)(instr[i][2] ^ (instr[i][2] & 0x40) << 1);
                    instr[i][3] = (byte)(instr[i][3] ^ (instr[i][3] & 0x40) << 1);
                    instr[i][11] >>= 4;     // slide
                }
                for (var i = 0; i < 51; i++)
                { // load tracklist
                    song[i] = br.ReadByte();
                    // if out of range, song ends here
                    if (
                      ((song[i] & 0x7F) > 0x31)
                      || ((song[i] & 0x7F) >= total_patterns_in_hsc)
                    ) song[i] = 0xFF;
                }
                var len = (fs.Length - fs.Position)/2/64/9;
                patterns = new hscnote[len, 64 * 9];
                for (var i = 0; i < len; i++)
                {     // load patterns
                    for (var j = 0; j < 64 * 9; j++)
                    {
                        patterns[i, j].note = br.ReadByte();
                        patterns[i, j].effect = br.ReadByte();
                    }
                }

                Rewind(0);          // rewind module
                return true;
            }
        }

        public bool Update()
        {
            // general vars
            byte chan, pattnr, note, effect, eff_op, inst, vol, Okt, db;
            ushort Fnr;
            long pattoff;

            del--;                      // player speed handling
            if (del != 0)
                return songend == 0;    // nothing done

            if (fadein != 0)         // fade-in handling
                fadein--;

            pattnr = song[songpos];
            // 0xff indicates song end, but this prevents a crash for some songs that
            // use other weird values, like 0xbf
            if (pattnr >= 0xb2)
            {     // arrangement handling
                songend = 1;        // set end-flag
                songpos = 0;
                pattnr = song[songpos];
            }
            else
              if (((pattnr & 128) != 0) && (pattnr <= 0xb1))
            { // goto pattern "nr"
                songpos = (byte)(song[songpos] & 127);
                pattpos = 0;
                pattnr = song[songpos];
                songend = 1;
            }

            pattoff = pattpos * 9;
            for (chan = 0; chan < 9; chan++)
            {     // handle all channels
                note = patterns[pattnr, pattoff].note;
                effect = patterns[pattnr, pattoff].effect;
                pattoff++;

                if ((note & 128) != 0)
                {                    // set instrument
                    SetInstr(chan, effect);
                    continue;
                }
                eff_op = (byte)(effect & 0x0f);
                inst = channel[chan].inst;
                if (note != 0)
                    channel[chan].slide = 0;

                switch (effect & 0xf0)
                {     // effect handling
                    case 0:               // global effect
                                          /* The following fx are unimplemented on purpose:
                                           * 02 - Slide Mainvolume up
                                           * 03 - Slide Mainvolume down (here: fade in)
                                           * 04 - Set Mainvolume to 0
                                           *
                                           * This is because i've never seen any HSC modules using the fx this way.
                                           * All modules use the fx the way, i've implemented it.
                                           */
                        switch (eff_op)
                        {
                            case 1: pattbreak++; break; // jump to next pattern
                            case 3: fadein = 31; break; // fade in (divided by 2)
                            case 5: mode6 = 1; break; // 6 voice mode on
                            case 6: mode6 = 0; break; // 6 voice mode off
                        }
                        break;
                    case 0x20:
                    case 0x10:                        // manual slides
                        if ((effect & 0x10) != 0)
                        {
                            channel[chan].freq += eff_op;
                            channel[chan].slide = (sbyte)(channel[chan].slide + eff_op);
                        }
                        else
                        {
                            channel[chan].freq -= eff_op;
                            channel[chan].slide = (sbyte)(channel[chan].slide - eff_op);
                        }
                        if (note == 0)
                            SetFreq(chan, channel[chan].freq);
                        break;
                    case 0x50:              // set percussion instrument (unimplemented)
                        break;
                    case 0x60:              // set feedback
                        Opl.WriteReg(0xc0 + chan, (instr[channel[chan].inst][8] & 1) + (eff_op << 1));
                        break;
                    case 0xa0:                        // set carrier volume
                        vol = (byte)(eff_op << 2);
                        Opl.WriteReg(0x43 + OplHelper.op_table[chan], vol | (instr[channel[chan].inst][2] & ~63));
                        break;
                    case 0xb0:                        // set modulator volume
                        vol = (byte)(eff_op << 2);
                        if ((instr[inst][8] & 1) != 0)
                            Opl.WriteReg(0x40 + OplHelper.op_table[chan], vol | (instr[channel[chan].inst][3] & ~63));
                        else
                            Opl.WriteReg(0x40 + OplHelper.op_table[chan], vol | (instr[inst][3] & ~63));
                        break;
                    case 0xc0:                        // set instrument volume
                        db = (byte)(eff_op << 2);
                        Opl.WriteReg(0x43 + OplHelper.op_table[chan], db | (instr[channel[chan].inst][2] & ~63));
                        if ((instr[inst][8] & 1) != 0)
                            Opl.WriteReg(0x40 + OplHelper.op_table[chan], db | (instr[channel[chan].inst][3] & ~63));
                        break;
                    case 0xd0: pattbreak++; songpos = eff_op; songend = 1; break; // position jump
                    case 0xf0:              // set speed
                        speed = eff_op;
                        del = ++speed;
                        break;
                }

                if (fadein != 0)           // fade-in volume setting
                    SetVolume(chan, fadein * 2, fadein * 2);

                if (note == 0)            // note handling
                    continue;
                note--;

                if ((note == 0x7f - 1) || (((note / 12) & ~7) != 0))
                {    // pause (7fh)
                    adl_freq[chan] = (byte)(adl_freq[chan] & ~32);
                    Opl.WriteReg(0xb0 + chan, adl_freq[chan]);
                    continue;
                }

                // play the note
                if (mtkmode != 0)    // imitate MPU-401 Trakker bug
                    note--;
                Okt = (byte)(((note / 12) & 7) << 2);
                Fnr = (ushort)(OplHelper.note_table[(note % 12)] + instr[inst][11] + channel[chan].slide);
                channel[chan].freq = Fnr;
                if (mode6 == 0 || chan < 6)
                    adl_freq[chan] = (byte)(Okt | 32);
                else
                    adl_freq[chan] = Okt;   // never set key for drums
                Opl.WriteReg(0xb0 + chan, 0);
                SetFreq(chan, Fnr);
                if (mode6 != 0)
                {
                    switch (chan)
                    {   // play drums
                        case 6: Opl.WriteReg(0xbd, bd & ~16); bd |= 48; break;  // bass drum
                        case 7: Opl.WriteReg(0xbd, bd & ~1); bd |= 33; break; // hihat
                        case 8: Opl.WriteReg(0xbd, bd & ~2); bd |= 34; break; // cymbal
                    }
                    Opl.WriteReg(0xbd, bd);
                }
            }

            del = speed;    // player speed-timing
            if (pattbreak != 0)
            {   // do post-effect handling
                pattpos = 0;      // pattern break!
                pattbreak = 0;
                songpos++;
                songpos %= 50;
                if (songpos == 0)
                    songend = 1;
            }
            else
            {
                pattpos++;
                pattpos &= 63;    // advance in pattern data
                if (pattpos == 0)
                {
                    songpos++;
                    songpos %= 50;
                    if (songpos == 0)
                        songend = 1;
                }
            }
            return songend == 0;    // still playing
        }

        private void Rewind(int subsong)
        {
            // rewind HSC player
            pattpos = 0; songpos = 0; pattbreak = 0; speed = 2;
            del = 1; songend = 0; mode6 = 0; bd = 0; fadein = 0;

            Opl.WriteReg(1, 32); Opl.WriteReg(8, 128); Opl.WriteReg(0xbd, 0);

            for (var i = 0; i < 9; i++)
                SetInstr((byte)i, (byte)i); // init channels
        }

        private void SetFreq(byte chan, ushort freq)
        {
            adl_freq[chan] = (byte)((adl_freq[chan] & ~3) | (freq >> 8));

            Opl.WriteReg(0xa0 + chan, freq & 0xff);
            Opl.WriteReg(0xb0 + chan, adl_freq[chan]);
        }

        private void SetInstr(byte chan, byte insnr)
        {
            byte[] ins = instr[insnr];
            byte op = OplHelper.op_table[chan];

            channel[chan].inst = insnr;   // set internal instrument
            Opl.WriteReg(0xb0 + chan, 0);     // stop old note

            // set instrument
            Opl.WriteReg(0xc0 + chan, ins[8]);
            Opl.WriteReg(0x23 + op, ins[0]);        // carrier
            Opl.WriteReg(0x20 + op, ins[1]);        // modulator
            Opl.WriteReg(0x63 + op, ins[4]);        // bits 0..3 = decay; 4..7 = attack
            Opl.WriteReg(0x60 + op, ins[5]);
            Opl.WriteReg(0x83 + op, ins[6]);        // 0..3 = release; 4..7 = sustain
            Opl.WriteReg(0x80 + op, ins[7]);
            Opl.WriteReg(0xe3 + op, ins[9]);        // bits 0..1 = Wellenform
            Opl.WriteReg(0xe0 + op, ins[10]);
            SetVolume(chan, ins[2] & 63, ins[3] & 63);
        }

        private void SetVolume(byte chan, int volc, int volm)
        {
            var ins = instr[channel[chan].inst];
            byte op = OplHelper.op_table[chan];

            Opl.WriteReg(0x43 + op, volc | (ins[2] & ~63));
            if ((ins[8] & 1) != 0)             // carrier
                Opl.WriteReg(0x40 + op, volm | (ins[3] & ~63));
            else
                Opl.WriteReg(0x40 + op, ins[3]);    // modulator
        }
    }
}
