//
//  XsmPlayer.cs
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
    /// eXtra Simple Music Player, by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// </summary>
    internal sealed class XsmPlayer : IMusicPlayer
    {
        struct Instrument
        {
            public byte[] value;
        }

        private ushort songlen;
        private byte[] music;
        private int last, notenum;
        private bool songend;
        private Instrument[] inst;

        public IOpl Opl { get; }

        public float RefreshRate { get { return 5.0f; } }

        public XsmPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
            inst = new Instrument[9];
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
            var br = new BinaryReader(stream);

            // check if header matches
            var id = new string(br.ReadChars(6)); songlen = br.ReadUInt16();
            if (!string.Equals(id, "ofTAZ!", StringComparison.Ordinal) || songlen > 3200) return false;

            // read and set instruments
            for (var i = 0; i < 9; i++)
            {
                inst[i] = new Instrument();
                inst[i].value = br.ReadBytes(11);
                stream.Seek(5, SeekOrigin.Current);
            }

            // read song data
            music = new byte[songlen * 9];
            for (var i = 0; i < 9; i++)
                for (var j = 0; j < songlen; j++)
                    music[j * 9 + i] = br.ReadByte();

            // success
            Rewind(0);
            return true;
        }

        private void Rewind(int subsong)
        {
            notenum = last = 0;
            songend = false;
            Opl.WriteReg(1, 32);
            for (var i = 0; i < 9; i++)
            {
                Opl.WriteReg(0x20 + OplHelper.op_table[i], inst[i].value[0]);
                Opl.WriteReg(0x23 + OplHelper.op_table[i], inst[i].value[1]);
                Opl.WriteReg(0x40 + OplHelper.op_table[i], inst[i].value[2]);
                Opl.WriteReg(0x43 + OplHelper.op_table[i], inst[i].value[3]);
                Opl.WriteReg(0x60 + OplHelper.op_table[i], inst[i].value[4]);
                Opl.WriteReg(0x63 + OplHelper.op_table[i], inst[i].value[5]);
                Opl.WriteReg(0x80 + OplHelper.op_table[i], inst[i].value[6]);
                Opl.WriteReg(0x83 + OplHelper.op_table[i], inst[i].value[7]);
                Opl.WriteReg(0xe0 + OplHelper.op_table[i], inst[i].value[8]);
                Opl.WriteReg(0xe3 + OplHelper.op_table[i], inst[i].value[9]);
                Opl.WriteReg(0xc0 + OplHelper.op_table[i], inst[i].value[10]);
            }
        }

        public bool Update()
        {
            if (notenum >= songlen)
            {
                songend = true;
                notenum = last = 0;
            }

            for (var c = 0; c < 9; c++)
                if (music[notenum * 9 + c] != music[last * 9 + c])
                    Opl.WriteReg(0xb0 + c, 0);

            for (var c = 0; c < 9; c++)
            {
                if (music[notenum * 9 + c] != 0)
                    PlayNote(c, music[notenum * 9 + c] % 12, music[notenum * 9 + c] / 12);
                else
                    PlayNote(c, 0, 0);
            }

            last = notenum;
            notenum++;
            return !songend;
        }

        private void PlayNote(int c, int note, int octv)
        {
            int freq = OplHelper.note_table[note];

            if (note == 0 && octv == 0) freq = 0;
            Opl.WriteReg(0xa0 + c, freq & 0xff);
            Opl.WriteReg(0xb0 + c, (freq / 0xff) | 32 | (octv * 4));
        }
    }
}
