//
//  S3mPlayer.cs
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
    /// S3M Player by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class S3mPlayer : IMusicPlayer
    {
        private static readonly sbyte[] chnresolv =	// S3M . adlib channel conversion
            { -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,0,1,2,3,4,5,6,7,8,-1,-1,-1,-1,-1,-1,-1};

        private static readonly ushort[] notetable =        // S3M adlib note table
            { 340,363,385,408,432,458,485,514,544,577,611,647};

        private static readonly byte[] vibratotab =        // vibrato rate table
            { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,16,15,14,13,12,11,10,9,8,7,6,5,4,3,2,1};

        struct S3m_header
        {
            public string name;              // song name
            public byte kennung, typ;
            public ushort ordnum, insnum, patnum, flags, cwtv, ffi;
            public string scrm;
            public byte gv, @is, it, mv, uc, dp;
            public ushort special;
            public byte[] chanset;
        }
        struct S3mInst
        {
            public byte type;
            public string filename;
            public byte d00, d01, d02, d03, d04, d05, d06, d07, d08, d09, d0a, d0b, volume, dsk;
            public ulong c2spd;
            public string name;
            public string scri;
        }
        struct S3mPattern
        {
            public byte note, oct, instrument, volume, command, info;
        }
        struct S3mChannel
        {
            public ushort freq, nextfreq;
            public byte oct, vol, _inst, fx, info, dualinfo, key, nextoct, trigger, note;
        }

        private S3m_header _header;
        private S3mPattern[,,] _pattern = new S3mPattern[99, 64, 32];
        private S3mInst[] _inst;
        private S3mChannel[] _channel;

        private byte[] _orders;
        private byte _crow, _ord, _speed, _tempo, _del, _songend, _loopstart, _loopcnt;

        public IOpl Opl { get; }

        public float RefreshRate { get { return _tempo / 2.5f; } }

        public S3mPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;

            _channel = new S3mChannel[9];
            _orders = new byte[256];
            _inst = new S3mInst[99];
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var br = new BinaryReader(fs);
                var insptr = new ushort[99];
                var pattptr = new ushort[99];
                int row;
                byte bufval, bufval2;
                ushort ppatlen;
                var adlibins = false;

                // file validation section
                var checkhead = LoadHeader(br);
                if (checkhead.kennung != 0x1a || checkhead.typ != 16
                   || checkhead.insnum > 99)
                {
                    return false;
                }
                if (checkhead.scrm != "SCRM")
                {
                    return false;
                }
                fs.Seek(checkhead.ordnum, SeekOrigin.Current);
                for (var i = 0; i < checkhead.insnum; i++)
                    insptr[i] = br.ReadUInt16();
                for (var i = 0; i < checkhead.insnum; i++)
                {
                    fs.Seek(insptr[i] * 16, SeekOrigin.Begin);
                    if (br.ReadByte() >= 2)
                    {
                        adlibins = true;
                        break;
                    }
                }
                if (!adlibins) return false;

                // load section
                fs.Seek(0, SeekOrigin.Begin); // rewind for load
                _header = LoadHeader(br);     // read header

                // security check
                if (_header.ordnum > 256 || _header.insnum > 99 || _header.patnum > 99)
                {
                    return false;
                }

                for (var i = 0; i < _header.ordnum; i++) _orders[i] = br.ReadByte();    // read orders
                for (var i = 0; i < _header.insnum; i++) insptr[i] = br.ReadUInt16();  // instrument parapointers
                for (var i = 0; i < _header.patnum; i++) pattptr[i] = br.ReadUInt16(); // pattern parapointers

                for (var i = 0; i < _header.insnum; i++)
                {   // load instruments
                    fs.Seek(insptr[i] * 16, SeekOrigin.Begin);
                    _inst[i].type = br.ReadByte();
                    _inst[i].filename = new string(br.ReadChars(15));
                    _inst[i].d00 = br.ReadByte(); _inst[i].d01 = br.ReadByte();
                    _inst[i].d02 = br.ReadByte(); _inst[i].d03 = br.ReadByte();
                    _inst[i].d04 = br.ReadByte(); _inst[i].d05 = br.ReadByte();
                    _inst[i].d06 = br.ReadByte(); _inst[i].d07 = br.ReadByte();
                    _inst[i].d08 = br.ReadByte(); _inst[i].d09 = br.ReadByte();
                    _inst[i].d0a = br.ReadByte(); _inst[i].d0b = br.ReadByte();
                    _inst[i].volume = br.ReadByte(); _inst[i].dsk = br.ReadByte();
                    fs.Seek(2, SeekOrigin.Current);
                    _inst[i].c2spd = br.ReadUInt32();
                    fs.Seek(12, SeekOrigin.Current);
                    _inst[i].name = new string(br.ReadChars(28));
                    _inst[i].scri = new string(br.ReadChars(4));
                }

                for (var i = 0; i < _header.patnum; i++)
                {   // depack patterns
                    fs.Seek(pattptr[i] * 16, SeekOrigin.Begin);
                    ppatlen = br.ReadUInt16();
                    long pattpos = fs.Position;
                    for (row = 0; (row < 64) && (pattpos - pattptr[i] * 16 <= ppatlen); row++)
                        do
                        {
                            bufval = br.ReadByte();
                            if ((bufval & 32) != 0)
                            {
                                bufval2 = br.ReadByte();
                                _pattern[i, row, bufval & 31].note = (byte)(bufval2 & 15);
                                _pattern[i, row, bufval & 31].oct = (byte)((bufval2 & 240) >> 4);
                                _pattern[i, row, bufval & 31].instrument = br.ReadByte();
                            }
                            if ((bufval & 64) != 0)
                                _pattern[i, row, bufval & 31].volume = br.ReadByte();
                            if ((bufval & 128) != 0)
                            {
                                _pattern[i, row, bufval & 31].command = br.ReadByte();
                                _pattern[i, row, bufval & 31].info = br.ReadByte();
                            }
                        } while (bufval != 0);
                }

                Rewind(0);
                return true;        // done
            }
        }

        public bool Update()
        {
            byte pattbreak = 0, donote;        // remember vars
            byte pattnr, chan, row, info;  // cache vars
            sbyte realchan;

            // effect handling (timer dependant)
            for (realchan = 0; realchan < 9; realchan++)
            {
                info = _channel[realchan].info;  // fill infobyte cache
                switch (_channel[realchan].fx)
                {
                    case 11:
                    case 12:
                    case 4:
                        if (_channel[realchan].fx == 11) // dual command: H00 and Dxy
                            Vibrato((byte)realchan, _channel[realchan].dualinfo);
                        else if (_channel[realchan].fx == 12)                // dual command: G00 and Dxy
                            TonePortamento((byte)realchan, _channel[realchan].dualinfo);

                        if (info <= 0x0f)
                        {           // volume slide down
                            if (_channel[realchan].vol - info >= 0)
                                _channel[realchan].vol -= info;
                            else
                                _channel[realchan].vol = 0;
                        }
                        if ((info & 0x0f) == 0)
                        {           // volume slide up
                            if (_channel[realchan].vol + (info >> 4) <= 63)
                                _channel[realchan].vol += (byte)(info >> 4);
                            else
                                _channel[realchan].vol = 63;
                        }
                        SetVolume((byte)realchan);
                        break;
                    case 5:
                        if (info == 0xf0 || info <= 0xe0)
                        {   // slide down
                            SlideDown((byte)realchan, info);
                            SetFreq((byte)realchan);
                        }
                        break;
                    case 6:
                        if (info == 0xf0 || info <= 0xe0)
                        {   // slide up
                            SlideUp((byte)realchan, info);
                            SetFreq((byte)realchan);
                        }
                        break;
                    case 7: TonePortamento((byte)realchan, _channel[realchan].dualinfo); break;   // tone portamento
                    case 8: Vibrato((byte)realchan, _channel[realchan].dualinfo); break;   // vibrato
                    case 10:
                        _channel[realchan].nextfreq = _channel[realchan].freq;    // arpeggio
                        _channel[realchan].nextoct = _channel[realchan].oct;
                        switch (_channel[realchan].trigger)
                        {
                            case 0: _channel[realchan].freq = notetable[_channel[realchan].note]; break;
                            case 1:
                                if (_channel[realchan].note + ((info & 0xf0) >> 4) < 12)
                                    _channel[realchan].freq = notetable[_channel[realchan].note + ((info & 0xf0) >> 4)];
                                else
                                {
                                    _channel[realchan].freq = notetable[_channel[realchan].note + ((info & 0xf0) >> 4) - 12];
                                    _channel[realchan].oct++;
                                }
                                break;
                            case 2:
                                if (_channel[realchan].note + (info & 0x0f) < 12)
                                    _channel[realchan].freq = notetable[_channel[realchan].note + (info & 0x0f)];
                                else
                                {
                                    _channel[realchan].freq = notetable[_channel[realchan].note + (info & 0x0f) - 12];
                                    _channel[realchan].oct++;
                                }
                                break;
                        }
                        if (_channel[realchan].trigger < 2)
                            _channel[realchan].trigger++;
                        else
                            _channel[realchan].trigger = 0;
                        SetFreq((byte)realchan);
                        _channel[realchan].freq = _channel[realchan].nextfreq;
                        _channel[realchan].oct = _channel[realchan].nextoct;
                        break;
                    case 21: Vibrato((byte)realchan, (byte)(info / 4)); break;   // fine vibrato
                }
            }

            if (_del != 0)
            {       // speed compensation
                _del--;
                return _songend == 0;
            }

            // arrangement handling
            pattnr = _orders[_ord];
            if (pattnr == 0xff || _ord > _header.ordnum)
            {   // "--" end of song
                _songend = 1;                // set end-flag
                _ord = 0;
                pattnr = _orders[_ord];
                if (pattnr == 0xff)
                    return _songend == 0;
            }
            if (pattnr == 0xfe)
            {       // "++" skip marker
                _ord++; pattnr = _orders[_ord];
            }

            // play row
            row = _crow; // fill row cache
            for (chan = 0; chan < 32; chan++)
            {
                if ((_header.chanset[chan] & 128) == 0)      // resolve S3M -> AdLib channels
                    realchan = chnresolv[_header.chanset[chan] & 127];
                else
                    realchan = -1;      // channel disabled
                if (realchan != -1)
                {   // channel playable?
                    // set channel values
                    donote = 0;
                    if (_pattern[pattnr, row, chan].note < 14)
                    {
                        // tone portamento
                        if (_pattern[pattnr, row, chan].command == 7 || _pattern[pattnr, row, chan].command == 12)
                        {
                            _channel[realchan].nextfreq = notetable[_pattern[pattnr, row, chan].note];
                            _channel[realchan].nextoct = _pattern[pattnr, row, chan].oct;
                        }
                        else
                        {                                           // normal note
                            _channel[realchan].note = _pattern[pattnr, row, chan].note;
                            _channel[realchan].freq = notetable[_pattern[pattnr, row, chan].note];
                            _channel[realchan].oct = _pattern[pattnr, row, chan].oct;
                            _channel[realchan].key = 1;
                            donote = 1;
                        }
                    }
                    if (_pattern[pattnr, row, chan].note == 14)
                    {   // key off (is 14 here, cause note is only first 4 bits)
                        _channel[realchan].key = 0;
                        SetFreq((byte)realchan);
                    }
                    if ((_channel[realchan].fx != 8 && _channel[realchan].fx != 11) &&    // vibrato begins
                   (_pattern[pattnr, row, chan].command == 8 || _pattern[pattnr, row, chan].command == 11))
                    {
                        _channel[realchan].nextfreq = _channel[realchan].freq;
                        _channel[realchan].nextoct = _channel[realchan].oct;
                    }
                    if (_pattern[pattnr, row, chan].note >= 14)
                        if ((_channel[realchan].fx == 8 || _channel[realchan].fx == 11) &&    // vibrato ends
                           (_pattern[pattnr, row, chan].command != 8 && _pattern[pattnr, row, chan].command != 11))
                        {
                            _channel[realchan].freq = _channel[realchan].nextfreq;
                            _channel[realchan].oct = _channel[realchan].nextoct;
                            SetFreq((byte)realchan);
                        }
                    if (_pattern[pattnr, row, chan].instrument != 0)
                    {   // set instrument
                        _channel[realchan]._inst = (byte)(_pattern[pattnr, row, chan].instrument - 1);
                        if (_inst[_channel[realchan]._inst].volume < 64)

                            _channel[realchan].vol = _inst[_channel[realchan]._inst].volume;
                        else
                            _channel[realchan].vol = 63;
                        if (_pattern[pattnr, row, chan].command != 7)
                            donote = 1;
                    }
                    if (_pattern[pattnr, row, chan].volume != 255)
                    {
                        if (_pattern[pattnr, row, chan].volume < 64) // set volume

                            _channel[realchan].vol = _pattern[pattnr, row, chan].volume;
                        else
                            _channel[realchan].vol = 63;
                    }
                    _channel[realchan].fx = _pattern[pattnr, row, chan].command;  // set command
                    if (_pattern[pattnr, row, chan].info != 0)           // set infobyte
                        _channel[realchan].info = _pattern[pattnr, row, chan].info;

                    // some commands reset the infobyte memory
                    switch (_channel[realchan].fx)
                    {
                        case 1:
                        case 2:
                        case 3:
                        case 20:
                            _channel[realchan].info = _pattern[pattnr, row, chan].info;
                            break;
                    }

                    // play note
                    if (donote != 0)
                        PlayNote((byte)realchan);
                    if (_pattern[pattnr, row, chan].volume != 255)   // set volume
                        SetVolume((byte)realchan);

                    // command handling (row dependant)
                    info = _channel[realchan].info;  // fill infobyte cache
                    switch (_channel[realchan].fx)
                    {
                        case 1: _speed = info; break;    // set speed
                        case 2: if (info <= _ord) _songend = 1; _ord = info; _crow = 0; pattbreak = 1; break;   // jump to order
                        case 3: if (pattbreak == 0) { _crow = info; _ord++; pattbreak = 1; } break;   // pattern break
                        case 4:
                            if (info > 0xf0)
                            {       // fine volume down
                                if (_channel[realchan].vol - (info & 0x0f) >= 0)
                                    _channel[realchan].vol -= (byte)(info & 0x0f);
                                else
                                    _channel[realchan].vol = 0;
                            }
                            if ((info & 0x0f) == 0x0f && info >= 0x1f)
                            {   // fine volume up
                                if (_channel[realchan].vol + ((info & 0xf0) >> 4) <= 63)
                                    _channel[realchan].vol += (byte)((info & 0xf0) >> 4);
                                else
                                    _channel[realchan].vol = 63;
                            }
                            SetVolume((byte)realchan);
                            break;
                        case 5:
                            if (info > 0xf0)
                            {           // fine slide down
                                SlideDown((byte)realchan, (byte)(info & 0x0f));
                                SetFreq((byte)realchan);
                            }
                            if (info > 0xe0 && info < 0xf0)
                            {       // extra fine slide down
                                SlideDown((byte)realchan, (byte)((info & 0x0f) / 4));
                                SetFreq((byte)realchan);
                            }
                            break;
                        case 6:
                            if (info > 0xf0)
                            {               // fine slide up
                                SlideUp((byte)realchan, (byte)(info & 0x0f));
                                SetFreq((byte)realchan);
                            }
                            if (info > 0xe0 && info < 0xf0)
                            {       // extra fine slide up
                                SlideUp((byte)realchan, (byte)((info & 0x0f) / 4));
                                SetFreq((byte)realchan);
                            }
                            break;
                        case 7:                                                     // tone portamento
                        case 8:
                            if ((_channel[realchan].fx == 7 ||   // vibrato (remember info for dual commands)
                        _channel[realchan].fx == 8) && _pattern[pattnr, row, chan].info != 0)
                                _channel[realchan].dualinfo = info;
                            break;
                        case 10: _channel[realchan].trigger = 0; break;  // arpeggio (set trigger)
                        case 19:
                            if (info == 0xb0)               // set loop start
                                _loopstart = row;
                            if (info > 0xb0 && info <= 0xbf)
                            {       // pattern loop
                                if (_loopcnt == 0)
                                {
                                    _loopcnt = (byte)(info & 0x0f);
                                    _crow = _loopstart;
                                    pattbreak = 1;
                                }
                                else
                                  if (--_loopcnt > 0)
                                {
                                    _crow = _loopstart;
                                    pattbreak = 1;
                                }
                            }
                            if ((info & 0xf0) == 0xe0)          // patterndelay
                                _del = (byte)(_speed * (info & 0x0f) - 1);
                            break;
                        case 20: _tempo = info; break;           // set tempo
                    }
                }
            }

            if (_del == 0)
                _del = (byte)(_speed - 1);        // speed compensation
            if (pattbreak == 0)
            {       // next row (only if no manual advance)
                _crow++;
                if (_crow > 63)
                {
                    _crow = 0;
                    _ord++;
                    _loopstart = 0;
                }
            }

            return _songend == 0; // still playing
        }

        private void Rewind(int subsong)
        {
            // set basic variables
            _songend = 0; _ord = 0; _crow = 0; _tempo = _header.it;
            _speed = _header.@is; _del = 0; _loopstart = 0; _loopcnt = 0;

            Opl.WriteReg(1, 32);          // Go to ym3812 mode
        }

        private S3m_header LoadHeader(BinaryReader br)
        {
            var h = new S3m_header();
            h.name = new string(br.ReadChars(28));
            h.kennung = br.ReadByte(); h.typ = br.ReadByte();
            br.BaseStream.Seek(2, SeekOrigin.Current);
            h.ordnum = br.ReadUInt16(); h.insnum = br.ReadUInt16();
            h.patnum = br.ReadUInt16(); h.flags = br.ReadUInt16();
            h.cwtv = br.ReadUInt16(); h.ffi = br.ReadUInt16();
            h.scrm = new string(br.ReadChars(4));
            h.gv = br.ReadByte(); h.@is = br.ReadByte(); h.it = br.ReadByte();
            h.mv = br.ReadByte(); h.uc = br.ReadByte(); h.dp = br.ReadByte();
            br.BaseStream.Seek(8, SeekOrigin.Current);
            h.special = br.ReadUInt16();
            h.chanset = br.ReadBytes(32);
            return h;
        }

        private void SetVolume(byte chan)
        {
            byte op = OplHelper.op_table[chan], insnr = _channel[chan]._inst;

            Opl.WriteReg(0x43 + op, (int)(63 - ((63 - (_inst[insnr].d03 & 63)) / 63.0) * _channel[chan].vol) + (_inst[insnr].d03 & 192));
            if ((_inst[insnr].d0a & 1) != 0)
                Opl.WriteReg(0x40 + op, (int)(63 - ((63 - (_inst[insnr].d02 & 63)) / 63.0) * _channel[chan].vol) + (_inst[insnr].d02 & 192));
        }

        private void SetFreq(byte chan)
        {
            Opl.WriteReg(0xa0 + chan, _channel[chan].freq & 255);
            if (_channel[chan].key != 0)
                Opl.WriteReg(0xb0 + chan, (((_channel[chan].freq & 768) >> 8) + (_channel[chan].oct << 2)) | 32);
            else
                Opl.WriteReg(0xb0 + chan, ((_channel[chan].freq & 768) >> 8) + (_channel[chan].oct << 2));
        }

        private void PlayNote(byte chan)
        {
            byte op = OplHelper.op_table[chan], insnr = _channel[chan]._inst;

            Opl.WriteReg(0xb0 + chan, 0); // stop old note

            // set instrument data
            Opl.WriteReg(0x20 + op, _inst[insnr].d00);
            Opl.WriteReg(0x23 + op, _inst[insnr].d01);
            Opl.WriteReg(0x40 + op, _inst[insnr].d02);
            Opl.WriteReg(0x43 + op, _inst[insnr].d03);
            Opl.WriteReg(0x60 + op, _inst[insnr].d04);
            Opl.WriteReg(0x63 + op, _inst[insnr].d05);
            Opl.WriteReg(0x80 + op, _inst[insnr].d06);
            Opl.WriteReg(0x83 + op, _inst[insnr].d07);
            Opl.WriteReg(0xe0 + op, _inst[insnr].d08);
            Opl.WriteReg(0xe3 + op, _inst[insnr].d09);
            Opl.WriteReg(0xc0 + chan, _inst[insnr].d0a);

            // set frequency & play
            _channel[chan].key = 1;
            SetFreq(chan);
        }

        private void SlideDown(byte chan, byte amount)
        {
            if (_channel[chan].freq - amount > 340)
                _channel[chan].freq -= amount;
            else
              if (_channel[chan].oct > 0)
            {
                _channel[chan].oct--;
                _channel[chan].freq = 684;
            }
            else
                _channel[chan].freq = 340;
        }

        private void SlideUp(byte chan, byte amount)
        {
            if (_channel[chan].freq + amount < 686)
                _channel[chan].freq += amount;
            else
              if (_channel[chan].oct < 7)
            {
                _channel[chan].oct++;
                _channel[chan].freq = 341;
            }
            else
                _channel[chan].freq = 686;
        }

        private void Vibrato(byte chan, byte info)
        {
            var speed = (byte)(info >> 4);
            var depth = (byte)((info & 0x0f) / 2);

            for (var i = 0; i < speed; i++)
            {
                _channel[chan].trigger++;
                while (_channel[chan].trigger >= 64)
                    _channel[chan].trigger -= 64;
                if (_channel[chan].trigger >= 16 && _channel[chan].trigger < 48)
                    SlideDown(chan, (byte)(vibratotab[_channel[chan].trigger - 16] / (16 - depth)));
                if (_channel[chan].trigger < 16)
                    SlideUp(chan, (byte)(vibratotab[_channel[chan].trigger + 16] / (16 - depth)));
                if (_channel[chan].trigger >= 48)
                    SlideUp(chan, (byte)(vibratotab[_channel[chan].trigger - 48] / (16 - depth)));
            }
            SetFreq(chan);
        }

        private void TonePortamento(byte chan, byte info)
        {
            if (_channel[chan].freq + (_channel[chan].oct << 10) < _channel[chan].nextfreq +
               (_channel[chan].nextoct << 10))
                SlideUp(chan, info);
            if (_channel[chan].freq + (_channel[chan].oct << 10) > _channel[chan].nextfreq +
               (_channel[chan].nextoct << 10))
                SlideDown(chan, info);
            SetFreq(chan);
        }
    }
}
