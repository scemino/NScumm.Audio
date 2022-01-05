//
//  IdAdlPlayer.cs
//
//  Author:
//       Ben McLean <mclean.ben@gmail.com>
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

using NScumm.Core.Audio.OPL;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace NScumm.Audio.Players
{
    /// <summary>
    /// id Software Adlib Sound Effect Player by Ben McLean mclean.ben@gmail.com
    /// </summary>
    public class IdAdlPlayer : IMusicPlayer
    {
        public IOpl Opl { get; }

        public IdAdlPlayer(IOpl opl) => Opl = opl ?? throw new ArgumentNullException(nameof(opl));

        public float RefreshRate => 140f; // These sound effects play back at 140 Hz.

        public bool Load(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
                return Load(fs);
        }

        public bool Load(Stream stream)
        {
            try
            {
                CurrentSound = new Adl(stream);
                Opl.WriteReg(1, 32); // go to OPL2 mode
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool Note
        {
            get => note;
            set
            {
                if (note = value)
                    Opl.WriteReg(Adl.OctavePort, (byte)(CurrentSound.Block | Adl.KeyFlag));
                else
                    Opl.WriteReg(Adl.OctavePort, 0);
            }
        }
        private bool note = false;

        public IdAdlPlayer SetInstrument()
        {
            for (int i = 0; i < Adl.InstrumentPorts.Count; i++)
                Opl.WriteReg(Adl.InstrumentPorts[i], CurrentSound.Instrument[i]);
            Opl.WriteReg(0xC0, 0); // WOLF3D's code ignores this value in its sound data, always setting it to zero instead.
            return this;
        }

        public bool Update()
        {
            if (CurrentSound != null)
            {
                if (CurrentSound.Notes[CurrentNote] == 0)
                    Note = false;
                else
                {
                    if (!Note) Note = true;
                    Opl.WriteReg(Adl.NotePort, CurrentSound.Notes[CurrentNote]);
                }
                CurrentNote++;
                if (CurrentNote >= CurrentSound.Notes.Length)
                {
                    CurrentSound = null;
                    return false;
                }
                return true;
            }
            return false;
        }

        public uint CurrentNote = 0;

        public Adl CurrentSound
        {
            get => adl;
            set
            {
                if (adl == null || value == null || adl == value || value.Priority >= adl.Priority)
                {
                    CurrentNote = 0;
                    if (Opl != null)
                    {
                        Note = false; // Must send a signal to stop the previous sound before starting a new sound
                        if ((adl = value) != null)
                        {
                            SetInstrument();
                            Note = true;
                        }
                    }
                }
            }
        }
        private Adl adl;

        /// <summary>
        /// Parses and stores the Adlib sound effect format. http://www.shikadi.net/moddingwiki/Adlib_sound_effect
        /// This format is extracted from Adam Biser's Wolfenstein Data Compiler (WDC) with the file extension ".ADL"
        /// However, it is not related to the ADL format by Westwood.
        /// </summary>
        public class Adl
        {
            /// <summary>
            /// These sound effects play back at 140 Hz.
            /// </summary>
            public const float Hz = 1f / 140f;

            /// <summary>
            /// The OPL register settings for the instrument
            /// </summary>
            public readonly byte[] Instrument = new byte[16];

            /// <summary>
            /// Octave to play notes at
            /// </summary>
            public readonly byte Octave;

            /// <summary>
            /// Pitch data
            /// </summary>
            public readonly byte[] Notes;
            public readonly ushort Priority;
            public Adl(Stream stream) : this(new BinaryReader(stream))
            { }
            public Adl(BinaryReader binaryReader)
            {
                uint length = binaryReader.ReadUInt32();
                Priority = binaryReader.ReadUInt16();
                binaryReader.Read(Instrument, 0, Instrument.Length);
                Octave = binaryReader.ReadByte();
                Notes = new byte[length];
                binaryReader.Read(Notes, 0, Notes.Length);
            }
            public byte Block => (byte)((Octave & 7) << 2);
            public const byte KeyFlag = 0x20;
            public const byte NotePort = 0xA0;
            public const byte OctavePort = 0xB0;
            public static readonly ReadOnlyCollection<byte> InstrumentPorts = Array.AsReadOnly(new byte[]
            {
            0x20, // mChar 	0x20 	Modulator characteristics
            0x23, // cChar 	0x23 	Carrier characteristics
            0x40, // mScale 	0x40 	Modulator scale
            0x43, // cScale 	0x43 	Carrier scale
            0x60, // mAttack 	0x60 	Modulator attack/decay rate
            0x63, // cAttack 	0x63 	Carrier attack/decay rate
            0x80, // mSust 	0x80 	Modulator sustain
            0x83, // cSust 	0x83 	Carrier sustain
            0xE0, // mWave 	0xE0 	Modulator waveform
            0xE3, // cWave 	0xE3 	Carrier waveform
                  //0xC0, // nConn 	0xC0 	Feedback/connection (usually ignored and set to 0)
                  // voice 	- 	unknown (Muse-only)
                  // mode 	- 	unknown (Muse-only)
                  //UINT8[3] 	padding 	- 	Pad instrument definition up to 16 bytes
            });
        }
    }
}
