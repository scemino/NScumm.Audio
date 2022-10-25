using System;
using System.IO;
using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players
{
    internal sealed class LdsPlayer : IMusicPlayer
    {
        private struct SoundBank
        {
            public byte mod_misc, mod_vol, mod_ad, mod_sr, mod_wave,
                car_misc, car_vol, car_ad, car_sr, car_wave, feedback, keyoff,
                portamento, glide, finetune, vibrato, vibdelay, mod_trem, car_trem,
                tremwait, arpeggio;
            public byte[] arp_tab;
            public ushort start, size;
            public byte fms;
            public ushort transp;
            public byte midinst, midvelo, midkey, midtrans, middum1, middum2;
        }

        private struct ChannelCheat
        {
            public byte chandelay, sound;
            public ushort high;
        }
        
        private struct Channel
        {
            public ushort gototune, lasttune, packpos;
            public byte finetune, glideto, portspeed, nextvol, volmod, volcar,
                vibwait, vibspeed, vibrate, trmstay, trmwait, trmspeed, trmrate, trmcount,
                trcwait, trcspeed, trcrate, trccount, arp_size, arp_speed, keycount,
                vibcount, arp_pos, arp_count, packwait;
            public byte[] arp_tab;
            public ChannelCheat chancheat;
        }

        private struct Position
        {
            public ushort patnum;
            public byte transpose;
        }
        
        // Note frequency table (16 notes / octave)
        private static readonly ushort[] frequency = {
            343, 344, 345, 347, 348, 349, 350, 352, 353, 354, 356, 357, 358,
            359, 361, 362, 363, 365, 366, 367, 369, 370, 371, 373, 374, 375,
            377, 378, 379, 381, 382, 384, 385, 386, 388, 389, 391, 392, 393,
            395, 396, 398, 399, 401, 402, 403, 405, 406, 408, 409, 411, 412,
            414, 415, 417, 418, 420, 421, 423, 424, 426, 427, 429, 430, 432,
            434, 435, 437, 438, 440, 442, 443, 445, 446, 448, 450, 451, 453,
            454, 456, 458, 459, 461, 463, 464, 466, 468, 469, 471, 473, 475,
            476, 478, 480, 481, 483, 485, 487, 488, 490, 492, 494, 496, 497,
            499, 501, 503, 505, 506, 508, 510, 512, 514, 516, 518, 519, 521,
            523, 525, 527, 529, 531, 533, 535, 537, 538, 540, 542, 544, 546,
            548, 550, 552, 554, 556, 558, 560, 562, 564, 566, 568, 571, 573,
            575, 577, 579, 581, 583, 585, 587, 589, 591, 594, 596, 598, 600,
            602, 604, 607, 609, 611, 613, 615, 618, 620, 622, 624, 627, 629,
            631, 633, 636, 638, 640, 643, 645, 647, 650, 652, 654, 657, 659,
            662, 664, 666, 669, 671, 674, 676, 678, 681, 683
        };
        
        // Vibrato (sine) table
        private static readonly byte[] vibtab = {
            0, 13, 25, 37, 50, 62, 74, 86, 98, 109, 120, 131, 142, 152, 162,
            171, 180, 189, 197, 205, 212, 219, 225, 231, 236, 240, 244, 247,
            250, 252, 254, 255, 255, 255, 254, 252, 250, 247, 244, 240, 236,
            231, 225, 219, 212, 205, 197, 189, 180, 171, 162, 152, 142, 131,
            120, 109, 98, 86, 74, 62, 50, 37, 25, 13
        };
        
        // Tremolo (sine * sine) table
        private static readonly byte[] tremtab = {
            0, 0, 1, 1, 2, 4, 5, 7, 10, 12, 15, 18, 21, 25, 29, 33, 37, 42, 47,
            52, 57, 62, 67, 73, 79, 85, 90, 97, 103, 109, 115, 121, 128, 134,
            140, 146, 152, 158, 165, 170, 176, 182, 188, 193, 198, 203, 208,
            213, 218, 222, 226, 230, 234, 237, 240, 243, 245, 248, 250, 251,
            253, 254, 254, 255, 255, 255, 254, 254, 253, 251, 250, 248, 245,
            243, 240, 237, 234, 230, 226, 222, 218, 213, 208, 203, 198, 193,
            188, 182, 176, 170, 165, 158, 152, 146, 140, 134, 127, 121, 115,
            109, 103, 97, 90, 85, 79, 73, 67, 62, 57, 52, 47, 42, 37, 33, 29,
            25, 21, 18, 15, 12, 10, 7, 5, 4, 2, 1, 1, 0
        };

        // 'maxsound' is maximum number of patches (instruments)
        // 'maxpos' is maximum number of entries in position list (orderlist)
        private const ushort maxsound = 0x3f, maxpos = 0xff;

        private SoundBank[] soundbank;
        private Channel[] channel;
        private Position[] positions;
        private byte jumping, fadeonoff, allvolume, hardfade,
            tempo_now, pattplay, tempo, regbd, mode, pattlen;
        private byte[] fmchip, chandelay; 
        private ushort posplay, jumppos, speed;
        private ushort[] patterns;
        private bool playing, songlooped;
        private uint numpatch, numposi, patterns_size, mainvolume;
        
        public IOpl Opl { get; }

        public float RefreshRate { get => 1193182.0f / speed; }

        public LdsPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }
        
        public bool Load(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".lds", StringComparison.OrdinalIgnoreCase))
            {
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
            uint i, j;
            mode = br.ReadByte();

            if (mode > 2)
            {
                br.Close();
                return false;
            }

            speed = br.ReadUInt16();
            tempo = br.ReadByte();
            pattlen = br.ReadByte();
            chandelay = new byte[9];
            for (i = 0; i < 9; i++)
            {
                chandelay[i] = br.ReadByte();
            }
            regbd = br.ReadByte();

            /* load patches */
            numpatch = br.ReadUInt16();
            soundbank = new SoundBank[numpatch];
            for (i = 0; i < numpatch; i++)
            {
                soundbank[i] = new SoundBank();
                soundbank[i].mod_misc = br.ReadByte();
                soundbank[i].mod_vol = br.ReadByte();
                soundbank[i].mod_ad = br.ReadByte();
                soundbank[i].mod_sr = br.ReadByte();
                soundbank[i].mod_wave = br.ReadByte();
                soundbank[i].car_misc = br.ReadByte();
                soundbank[i].car_vol = br.ReadByte();
                soundbank[i].car_ad = br.ReadByte();
                soundbank[i].car_sr = br.ReadByte();
                soundbank[i].car_wave = br.ReadByte();
                soundbank[i].feedback = br.ReadByte();
                soundbank[i].keyoff = br.ReadByte();
                soundbank[i].portamento = br.ReadByte();
                soundbank[i].glide = br.ReadByte();
                soundbank[i].finetune = br.ReadByte();
                soundbank[i].vibrato = br.ReadByte();
                soundbank[i].vibdelay = br.ReadByte();
                soundbank[i].mod_trem = br.ReadByte();
                soundbank[i].car_trem = br.ReadByte();
                soundbank[i].tremwait = br.ReadByte();
                soundbank[i].arpeggio = br.ReadByte();
                soundbank[i].arp_tab = new byte[12];
                for (j = 0; j < 12; j++)
                {
                    soundbank[i].arp_tab[j] = br.ReadByte();
                }
                soundbank[i].start = br.ReadUInt16();
                soundbank[i].size = br.ReadUInt16();
                soundbank[i].fms = br.ReadByte();
                soundbank[i].transp = br.ReadUInt16();
                soundbank[i].midinst = br.ReadByte();
                soundbank[i].midvelo = br.ReadByte();
                soundbank[i].midkey = br.ReadByte();
                soundbank[i].midtrans = br.ReadByte();
                soundbank[i].middum1 = br.ReadByte();
                soundbank[i].middum2 = br.ReadByte();
            }

            numposi = br.ReadUInt16();
            positions = new Position[9 * numposi];
            for (i = 0; i < numposi; i++)
            {
                for (j = 0; j < 9; j++)
                {
                    /*
                     * patnum is a pointer inside the pattern space, but patterns are 16bit
                     * word fields anyway, so it ought to be an even number (hopefully) and
                     * we can just divide it by 2 to get our array index of 16bit words.
                     */
                    positions[i * 9 + j] = new Position
                    {
                        patnum = (ushort)(br.ReadUInt16() / 2),
                        transpose = br.ReadByte(),
                    };
                }
            }
            
            /* load patterns */
            br.BaseStream.Seek(2, SeekOrigin.Current); /* ignore # of digital sounds (dunno what this is for) */

            var t = (br.BaseStream.Length - br.BaseStream.Position) / 2 + 1;
            patterns = new ushort[t];

            for (i = 0; br.PeekChar() != -1; i++)
            {
                patterns[i] = br.ReadUInt16();
            }
            
            Rewind(0);
            return true;
        }

        private void Rewind(int subsong)
        {
            // init all with 0
            tempo_now = 3; playing = true; songlooped = false;
            jumping = fadeonoff = allvolume = hardfade = pattplay = 0;
            posplay = jumppos = 0;
            mainvolume = 0;
            channel = new Channel[9];
            fmchip = new byte[255];
            for (var i = 0; i < 255; i++)
            {
                fmchip[i] = 0;
            }

            // OPL2 init
            Opl.WriteReg(1, 0x20);
            Opl.WriteReg(8, 0);
            Opl.WriteReg(0xbd, regbd);

            for (var i = 0; i < 9; i++)
            {
                channel[i] = new Channel();
                Opl.WriteReg(0x20 + OplHelper.op_table[i], 0);
                Opl.WriteReg(0x23 + OplHelper.op_table[i], 0);
                Opl.WriteReg(0x40 + OplHelper.op_table[i], 0x3f);
                Opl.WriteReg(0x43 + OplHelper.op_table[i], 0x3f);
                Opl.WriteReg(0x60 + OplHelper.op_table[i], 0xff);
                Opl.WriteReg(0x63 + OplHelper.op_table[i], 0xff);
                Opl.WriteReg(0x80 + OplHelper.op_table[i], 0xff);
                Opl.WriteReg(0x83 + OplHelper.op_table[i], 0xff);
                Opl.WriteReg(0xe0 + OplHelper.op_table[i], 0);
                Opl.WriteReg(0xe3 + OplHelper.op_table[i], 0);
                Opl.WriteReg(0xa0 + i, 0);
                Opl.WriteReg(0xb0 + i, 0);
                Opl.WriteReg(0xc0 + i, 0);
            }
        }

        public bool Update()
        {
            ushort freq, octave, chan, tune, wibc, tremc, arpreg;
            bool vbreak;
            byte level;
            int i;

            if (!playing)
            {
                return false;
            }

            // handle fading
            if (fadeonoff > 0)
            {
                if (fadeonoff <= 128)
                {
                    if (allvolume > fadeonoff || allvolume == 0)
                    {
                        allvolume -= fadeonoff;
                    }
                    else
                    {
                        allvolume = 1;
                        fadeonoff = 0;
                        if (hardfade != 0)
                        {
                            playing = false;
                            hardfade = 0;
                            for (i = 0; i < 9; i++)
                            {
                                channel[i].keycount = 1;
                            }
                        }
                    }
                }
                else if (((allvolume + (0x100 - fadeonoff)) & 0xff) <= mainvolume)
                {
                    allvolume += (byte) (0x100 - fadeonoff);
                }
                else
                {
                    allvolume = (byte)(mainvolume);
                    fadeonoff = 0;
                }
            }

            // handle channel delay
            for (chan = 0; chan < 9; chan++)
            {
                ref Channel c = ref channel[chan];
                if (c.chancheat.chandelay > 0)
                {
                    --c.chancheat.chandelay;
                    if (c.chancheat.chandelay <= 0)
                    {
                        PlaySound(c.chancheat.sound, chan, c.chancheat.high);
                    }
                }
            }

            // handle notes
            if (tempo_now <= 0)
            {
                vbreak = false;
                for (chan = 0; chan < 9; chan++)
                {
                    ref Channel c = ref channel[chan];
                    if (c.packwait <= 0)
                    {
                        var patnum = positions[posplay * 9 + chan].patnum;
                        var transpose = positions[posplay * 9 + chan].transpose;

                        var comword = patterns[patnum + c.packpos];
                        var comhi = (byte)(comword >> 8);
                        var comlo = (byte)(comword & 0xff);

                        if (comword > 0)
                        {
                            if (comhi == 0x80)
                            {
                                c.packwait = comlo;
                            }
                            else if (comhi >= 0x80)
                            {
                                switch (comhi)
                                {
                                    case 0xff:
                                        c.volcar = (byte) ((((c.volcar & 0x3f) * comlo) >> 6) & 0x3f);
                                        if ((fmchip[0xc0 + chan] & 1) > 0)
                                        {
                                            c.volmod = (byte) ((((c.volmod & 0x3f) * comlo) >> 6) & 0x3f);
                                        }
                                        break;
                                    case 0xfe:
                                        tempo = (byte) (comword & 0x3f);
                                        break;
                                    case 0xfd:
                                        c.nextvol = comlo;
                                        break;
                                    case 0xfc:
                                        playing = false;
                                        // in real player there's also full keyoff here, but we don't need it
                                        break;
                                    case 0xfb:
                                        c.keycount = 1;
                                        break;
                                    case 0xfa:
                                        vbreak = true;
                                        jumppos = (ushort) ((posplay + 1) & maxpos);
                                        break;
                                    case 0xf9:
                                        vbreak = true;
                                        jumppos = (ushort) (comlo & maxpos);
                                        jumping = 1;
                                        if (jumppos < posplay)
                                        {
                                            songlooped = true;
                                        }
                                        break;
                                    case 0xf8:
                                        c.lasttune = 0;
                                        break;
                                    case 0xf7:
                                        c.vibwait = 0;
                                        // PASCAL: c.vibspeed = ((comlo >> 4) & 15) + 2;
                                        c.vibspeed = (byte) ((comlo >> 4) + 2);
                                        c.vibrate = (byte) ((comlo & 15) + 1);
                                        break;
                                    case 0xf6:
                                        c.glideto = comlo;
                                        break;
                                    case 0xf5:
                                        c.finetune = comlo;
                                        break;
                                    case 0xf4:
                                        if (hardfade <= 0)
                                        {
                                            allvolume = (byte) (mainvolume = comlo);
                                            fadeonoff = 0;
                                        }

                                        break;
                                    case 0xf3:
                                        if (hardfade <= 0)
                                        {
                                            fadeonoff = comlo;
                                        }

                                        break;
                                    case 0xf2:
                                        c.trmstay = comlo;
                                        break;
                                    case 0xf1: // panorama
                                    case 0xf0: // progch
                                        // MIDI commands (unhandled)
//                                        AdPlug_LogWrite("CldsPlayer(): not handling MIDI command 0x%x, ""value = 0x%x\n", comhi);
                                        break;
                                    default:
                                        if (comhi < 0xa0)
                                        {
                                            c.glideto = (byte) (comhi & 0x1f);
                                        }
                                        else
                                        {
//                                            AdPlug_LogWrite("CldsPlayer(): unknown command 0x%x encountered!"" value = 0x%x\n", comhi, comlo);
                                        }

                                        break;
                                }
                            }
                            else
                            {
                                byte sound;
                                ushort high;
                                sbyte transp = (sbyte) (transpose & 127);

                                /*
                                 * Originally, in assembler code, the player first shifted
                                 * logically left the transpose byte by 1 and then shifted
                                 * arithmetically right the same byte to achieve the final,
                                 * signed transpose value. Since we can't do arithmetic shifts
                                 * in C, we just duplicate the 7th bit into the 8th one and
                                 * discard the 8th one completely.
                                 */

                                if ((transpose & 64) > 0)
                                {
                                    transp += 64;
                                    transp += 64;
                                }

                                if ((transpose & 128) > 0)
                                {
                                    sound = (byte) ((comlo + transp) & maxsound);
                                    high = (ushort) (comhi << 4);
                                }
                                else
                                {
                                    sound = (byte) (comlo & maxsound);
                                    high = (ushort) ((comhi + transp) << 4);
                                }

                                /*
                                PASCAL:
                                  sound = comlo & maxsound;
                                  high = (comhi + (((transpose + 0x24) & 0xff) - 0x24)) << 4;
                                  */

                                if (chandelay[chan] <= 0)
                                {
                                    PlaySound(sound, chan, high);
                                }
                                else
                                {
                                    c.chancheat.chandelay = chandelay[chan];
                                    c.chancheat.sound = sound;
                                    c.chancheat.high = high;
                                }
                            }
                        }

                        c.packpos++;
                    }
                    else
                    {
                        c.packwait--;
                    }
                }

                tempo_now = tempo;
                /*
                  The continue table is updated here, but this is only used in the
                  original player, which can be paused in the middle of a song and then
                  unpaused. Since AdPlug does all this for us automatically, we don't
                  have a continue table here. The continue table update code is noted
                  here for reference only.
            
                  if(!pattplay) {
                    conttab[speed & maxcont].position = posplay & 0xff;
                    conttab[speed & maxcont].tempo = tempo;
                  }
                */
                pattplay++;
                if (vbreak)
                {
                    pattplay = 0;
                    for (i = 0; i < 9; i++)
                    {
                        channel[i].packpos = channel[i].packwait = 0;
                    }
                    posplay = jumppos;
                }
                else if (pattplay >= pattlen)
                {
                    pattplay = 0;
                    for (i = 0; i < 9; i++)
                    {
                        channel[i].packpos = channel[i].packwait = 0;
                    }
                    posplay = (ushort)((posplay + 1) & maxpos);
                }
            }
            else
            {
                tempo_now--;
            }

            // make effects
            for (chan = 0; chan < 9; chan++)
            {
                ref Channel c = ref channel[chan];
                var regnum = OplHelper.op_table[chan];
                if (c.keycount > 0)
                {
                    if (c.keycount == 1)
                    {
                        SetRegsAdv((byte)(0xb0 + chan), 0xdf, 0);
                    }

                    c.keycount--;
                }

                // arpeggio
                if (c.arp_size == 0)
                {
                    arpreg = 0;
                }
                else
                {
                    arpreg = (ushort)(c.arp_tab[c.arp_pos] << 4);
                    if (arpreg == 0x800)
                    {
                        if (c.arp_pos > 0)
                        {
                            c.arp_tab[0] = c.arp_tab[c.arp_pos - 1];
                        }
                        c.arp_size = 1;
                        c.arp_pos = 0;
                        arpreg = (ushort)(c.arp_tab[0] << 4);
                    }

                    if (c.arp_count == c.arp_speed)
                    {
                        c.arp_pos++;
                        if (c.arp_pos >= c.arp_size)
                        {
                            c.arp_pos = 0;
                        }
                        c.arp_count = 0;
                    }
                    else
                    {
                        c.arp_count++;
                    }
                }

                // glide & portamento
                if (c.lasttune > 0 && (c.lasttune != c.gototune))
                {
                    if (c.lasttune > c.gototune)
                    {
                        if (c.lasttune - c.gototune < c.portspeed)
                        {
                            c.lasttune = c.gototune;
                        }
                        else
                        {
                            c.lasttune -= c.portspeed;
                        }
                    }
                    else
                    {
                        if (c.gototune - c.lasttune < c.portspeed)
                        {
                            c.lasttune = c.gototune;
                        }
                        else
                        {
                            c.lasttune += c.portspeed;
                        }
                    }

                    if (arpreg >= 0x800)
                    {
                        arpreg = (ushort) (c.lasttune - (arpreg ^ 0xff0) - 16);
                    }
                    else
                    {
                        arpreg += c.lasttune;
                    }

                    freq = frequency[arpreg % (12 * 16)];
                    octave = (ushort)(arpreg / (12 * 16) - 1);
                    SetRegs((byte)(0xa0 + chan), (byte)(freq & 0xff));
                    SetRegsAdv((byte)(0xb0 + chan), 0x20, (byte)(((octave << 2) + (freq >> 8)) & 0xdf));
                }
                else
                {
                    // vibrato
                    if (c.vibwait <= 0)
                    {
                        if (c.vibrate > 0)
                        {
                            wibc = (ushort)(vibtab[c.vibcount & 0x3f] * c.vibrate);

                            if ((c.vibcount & 0x40) == 0)
                            {
                                tune = (ushort) (c.lasttune + (wibc >> 8));
                            }
                            else
                            {
                                tune = (ushort) (c.lasttune - (wibc >> 8));
                            }

                            if (arpreg >= 0x800)
                            {
                                tune = (ushort)(tune - (arpreg ^ 0xff0) - 16);
                            }
                            else
                            {
                                tune += arpreg;
                            }

                            freq = frequency[tune % (12 * 16)];
                            octave = (ushort)(tune / (12 * 16) - 1);
                            SetRegs((byte)(0xa0 + chan), (byte)(freq & 0xff));
                            SetRegsAdv((byte)(0xb0 + chan), 0x20, (byte)(((octave << 2) + (freq >> 8)) & 0xdf));
                            c.vibcount += c.vibspeed;
                        }
                        else if (c.arp_size != 0)
                        {
                            // no vibrato, just arpeggio
                            if (arpreg >= 0x800)
                            {
                                tune = (ushort) (c.lasttune - (arpreg ^ 0xff0) - 16);
                            }
                            else
                            {
                                tune = (ushort) (c.lasttune + arpreg);
                            }

                            freq = frequency[tune % (12 * 16)];
                            octave = (ushort)(tune / (12 * 16) - 1);
                            SetRegs((byte)(0xa0 + chan), (byte)(freq & 0xff));
                            SetRegsAdv((byte)(0xb0 + chan), 0x20, (byte)(((octave << 2) + (freq >> 8)) & 0xdf));
                        }
                    }
                    else
                    {
                        // no vibrato, just arpeggio
                        c.vibwait--;

                        if (c.arp_size != 0)
                        {
                            if (arpreg >= 0x800)
                            {
                                tune = (ushort) (c.lasttune - (arpreg ^ 0xff0) - 16);
                            }
                            else
                            {
                                tune = (ushort) (c.lasttune + arpreg);
                            }

                            freq = frequency[tune % (12 * 16)];
                            octave = (ushort)(tune / (12 * 16) - 1);
                            SetRegs((byte)(0xa0 + chan), (byte)(freq & 0xff));
                            SetRegsAdv((byte)(0xb0 + chan), 0x20, (byte)(((octave << 2) + (freq >> 8)) & 0xdf));
                        }
                    }
                }

                // tremolo (modulator)
                if (c.trmwait <= 0)
                {
                    if (c.trmrate > 0)
                    {
                        tremc = (ushort)(tremtab[c.trmcount & 0x7f] * c.trmrate);
                        if ((tremc >> 8) <= (c.volmod & 0x3f))
                        {
                            level = (byte) ((c.volmod & 0x3f) - (tremc >> 8));
                        }
                        else
                        {
                            level = 0;
                        }

                        if (allvolume != 0 && (fmchip[0xc0 + chan] & 1) > 0)
                        {
                            SetRegsAdv((byte) (0x40 + regnum), 0xc0, (byte) (((level * allvolume) >> 8) ^ 0x3f));
                        }
                        else
                        {
                            SetRegsAdv((byte) (0x40 + regnum), 0xc0, (byte) (level ^ 0x3f));
                        }

                        c.trmcount += c.trmspeed;
                    }
                    else if (allvolume != 0 && (fmchip[0xc0 + chan] & 1) > 0)
                    {
                        SetRegsAdv((byte) (0x40 + regnum), 0xc0, (byte) (((((c.volmod & 0x3f) * allvolume) >> 8) ^ 0x3f) & 0x3f));
                    }
                    else
                    {
                        SetRegsAdv((byte) (0x40 + regnum), 0xc0, (byte) ((c.volmod ^ 0x3f) & 0x3f));
                    }
                }
                else
                {
                    c.trmwait--;
                    if (allvolume != 0 && (fmchip[0xc0 + chan] & 1) > 0)
                    {
                        SetRegsAdv((byte) (0x40 + regnum), 0xc0, (byte) (((((c.volmod & 0x3f) * allvolume) >> 8) ^ 0x3f) & 0x3f));
                    }
                }

                // tremolo (carrier)
                if (c.trcwait <= 0)
                {
                    if (c.trcrate > 0)
                    {
                        tremc = (ushort)(tremtab[c.trccount & 0x7f] * c.trcrate);
                        if ((tremc >> 8) <= (c.volcar & 0x3f))
                        {
                            level = (byte) ((c.volcar & 0x3f) - (tremc >> 8));
                        }
                        else
                        {
                            level = 0;
                        }

                        if (allvolume != 0)
                        {
                            SetRegsAdv((byte) (0x43 + regnum), 0xc0, (byte) (((level * allvolume) >> 8) ^ 0x3f));
                        }
                        else
                        {
                            SetRegsAdv((byte) (0x43 + regnum), 0xc0, (byte) (level ^ 0x3f));
                        }

                        c.trccount += c.trcspeed;
                    }
                    else if (allvolume != 0)
                    {
                        SetRegsAdv((byte) (0x43 + regnum), 0xc0, (byte) (((((c.volcar & 0x3f) * allvolume) >> 8) ^ 0x3f) & 0x3f));
                    }
                    else
                    {
                        SetRegsAdv((byte) (0x43 + regnum), 0xc0, (byte) ((c.volcar ^ 0x3f) & 0x3f));
                    }
                }
                else
                {
                    c.trcwait--;
                    if (allvolume != 0)
                    {
                        SetRegsAdv((byte) (0x43 + regnum), 0xc0, (byte) (((((c.volcar & 0x3f) * allvolume) >> 8) ^ 0x3f) & 0x3f));
                    }
                }
            }

            return (!playing || songlooped) ? false : true;
        }

        private void PlaySound(int inst_number, int channel_number, int tunehigh)
        {
            ref Channel c = ref channel[channel_number]; // current channel
            ref SoundBank sb = ref soundbank[inst_number]; // current instrument
            uint regnum = OplHelper.op_table[channel_number]; // channel's OPL2 register
            byte volcalc;

            // set fine tune
            tunehigh += ((sb.finetune + c.finetune + 0x80) & 0xff) - 0x80;

            // arpeggio handling
            if (sb.arpeggio <= 0)
            {
                ushort arpcalc = (ushort)(sb.arp_tab[0] << 4);

                if (arpcalc > 0x800)
                {
                    tunehigh = tunehigh - (arpcalc ^ 0xff0) - 16;
                }
                else
                {
                    tunehigh += arpcalc;
                }
            }

            // glide handling
            if (c.glideto != 0)
            {
                c.gototune = (ushort)(tunehigh);
                c.portspeed = c.glideto;
                c.glideto = c.finetune = 0;
                return;
            }

            // set modulator registers
            SetRegs((byte)(0x20 + regnum), sb.mod_misc);
            volcalc = sb.mod_vol;
            if (c.nextvol <= 0 || (sb.feedback & 1) <= 0)
            {
                c.volmod = volcalc;
            }
            else
            {
                c.volmod = (byte) ((volcalc & 0xc0) | ((((volcalc & 0x3f) * c.nextvol) >> 6)));
            }

            if ((sb.feedback & 1) == 1 && allvolume != 0)
            {
                SetRegs((byte) (0x40 + regnum), (byte)(((c.volmod & 0xc0) | (((c.volmod & 0x3f) * allvolume) >> 8)) ^ 0x3f));
            }
            else
            {
                SetRegs((byte) (0x40 + regnum), (byte)(c.volmod ^ 0x3f));
            }

            SetRegs((byte)(0x60 + regnum), sb.mod_ad);
            SetRegs((byte)(0x80 + regnum), sb.mod_sr);
            SetRegs((byte)(0xe0 + regnum), sb.mod_wave);

            // Set carrier registers
            SetRegs((byte)(0x23 + regnum), sb.car_misc);
            volcalc = sb.car_vol;
            if (c.nextvol <= 0)
            {
                c.volcar = volcalc;
            }
            else
            {
                c.volcar = (byte)((volcalc & 0xc0) | ((((volcalc & 0x3f) * c.nextvol) >> 6)));
            }

            if (allvolume > 0)
            {
                SetRegs((byte) (0x43 + regnum), (byte)(((c.volcar & 0xc0) | (((c.volcar & 0x3f) * allvolume) >> 8)) ^ 0x3f));
            }
            else
            {
                SetRegs((byte) (0x43 + regnum), (byte)(c.volcar ^ 0x3f));
            }

            SetRegs((byte)(0x63 + regnum), sb.car_ad);
            SetRegs((byte)(0x83 + regnum), sb.car_sr);
            SetRegs((byte)(0xe3 + regnum), sb.car_wave);
            SetRegs((byte)(0xc0 + channel_number), sb.feedback);
            SetRegsAdv((byte)(0xb0 + channel_number), 0xdf, 0); // key off

            var freq = frequency[tunehigh % (12 * 16)];
            var octave = (byte)(tunehigh / (12 * 16) - 1);
            if (sb.glide <= 0)
            {
                if (sb.portamento <= 0 || c.lasttune <= 0)
                {
                    SetRegs((byte)(0xa0 + channel_number), (byte)(freq & 0xff));
                    SetRegs((byte)(0xb0 + channel_number), (byte)((octave << 2) + 0x20 + (freq >> 8)));
                    c.lasttune = c.gototune = (ushort)tunehigh;
                }
                else
                {
                    c.gototune = (ushort)tunehigh;
                    c.portspeed = sb.portamento;
                    SetRegsAdv((byte)(0xb0 + channel_number), 0xdf, 0x20); // key on
                }
            }
            else
            {
                SetRegs((byte)(0xa0 + channel_number), (byte)(freq & 0xff));
                SetRegs((byte)(0xb0 + channel_number), (byte)((octave << 2) + 0x20 + (freq >> 8)));
                c.lasttune = (ushort)tunehigh;
                c.gototune = (ushort)(tunehigh + ((sb.glide + 0x80) & 0xff) - 0x80); // set destination
                c.portspeed = sb.portamento;
            }

            if (sb.vibrato <= 0)
            {
                c.vibwait = c.vibspeed = c.vibrate = 0;
            }
            else
            {
                c.vibwait = sb.vibdelay;
                // PASCAL:    c.vibspeed = ((i.vibrato >> 4) & 15) + 1;
                c.vibspeed = (byte)((sb.vibrato >> 4) + 2);
                c.vibrate = (byte)((sb.vibrato & 15) + 1);
            }

            if ((c.trmstay & 0xf0) <= 0)
            {
                c.trmwait = (byte)((sb.tremwait & 0xf0) >> 3);
                // PASCAL:    c.trmspeed = (i.mod_trem >> 4) & 15;
                c.trmspeed = (byte)(sb.mod_trem >> 4);
                c.trmrate = (byte)(sb.mod_trem & 15);
                c.trmcount = 0;
            }

            if ((c.trmstay & 0x0f) <= 0)
            {
                c.trcwait = (byte)((sb.tremwait & 15) << 1);
                // PASCAL:    c.trcspeed = (i.car_trem >> 4) & 15;
                c.trcspeed = (byte)(sb.car_trem >> 4);
                c.trcrate = (byte)(sb.car_trem & 15);
                c.trccount = 0;
            }

            c.arp_size = (byte)(sb.arpeggio & 15);
            c.arp_speed = (byte)(sb.arpeggio >> 4);
            c.arp_tab = new byte[12];
            for (var j = 0; j < 12; j++)
            {
                c.arp_tab[j] = sb.arp_tab[j];
            }

            c.keycount = sb.keyoff;
            c.nextvol = c.glideto = c.finetune = c.vibcount = c.arp_pos = c.arp_count = 0;
        }
        
        private void SetRegs(byte reg, byte val)
        {
            if (fmchip[reg] == val)
            {
                return;
            }

            fmchip[reg] = val;
            Opl.WriteReg(reg, val);
        }

        private void SetRegsAdv(byte reg, byte mask, byte val)
        {
            SetRegs(reg, (byte)((fmchip[reg] & mask) | val));
        }
    }
}