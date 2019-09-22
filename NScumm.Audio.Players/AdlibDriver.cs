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
using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players
{
    internal sealed class AdlibDriver
    {
        const int CALLBACKS_PER_SECOND = 72;

        // This table holds the register offset for operator 1 for each of the nine
        // channels. To get the register offset for operator 2, simply add 3.
        static readonly byte[] _regOffset = {
        0x00, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x10, 0x11,
        0x12
        };

        // Given the size of this table, and the range of its values, it's probably the
        // F-Numbers (10 bits) for the notes of the 12-tone scale. However, it does not
        // match the table in the Adlib documentation I've seen.
        static readonly ushort[] _unkTable = {
        0x0134, 0x0147, 0x015A, 0x016F, 0x0184, 0x019C, 0x01B4, 0x01CE, 0x01E9,
        0x0207, 0x0225, 0x0246
        };

        static readonly byte[][] _unkTable2 = {
        _unkTable2_1,
        _unkTable2_2,
        _unkTable2_1,
        _unkTable2_2,
        _unkTable2_3,
        _unkTable2_2
        };

        static readonly byte[] _unkTable2_1 = {
        0x50, 0x50, 0x4F, 0x4F, 0x4E, 0x4E, 0x4D, 0x4D,
        0x4C, 0x4C, 0x4B, 0x4B, 0x4A, 0x4A, 0x49, 0x49,
        0x48, 0x48, 0x47, 0x47, 0x46, 0x46, 0x45, 0x45,
        0x44, 0x44, 0x43, 0x43, 0x42, 0x42, 0x41, 0x41,
        0x40, 0x40, 0x3F, 0x3F, 0x3E, 0x3E, 0x3D, 0x3D,
        0x3C, 0x3C, 0x3B, 0x3B, 0x3A, 0x3A, 0x39, 0x39,
        0x38, 0x38, 0x37, 0x37, 0x36, 0x36, 0x35, 0x35,
        0x34, 0x34, 0x33, 0x33, 0x32, 0x32, 0x31, 0x31,
        0x30, 0x30, 0x2F, 0x2F, 0x2E, 0x2E, 0x2D, 0x2D,
        0x2C, 0x2C, 0x2B, 0x2B, 0x2A, 0x2A, 0x29, 0x29,
        0x28, 0x28, 0x27, 0x27, 0x26, 0x26, 0x25, 0x25,
        0x24, 0x24, 0x23, 0x23, 0x22, 0x22, 0x21, 0x21,
        0x20, 0x20, 0x1F, 0x1F, 0x1E, 0x1E, 0x1D, 0x1D,
        0x1C, 0x1C, 0x1B, 0x1B, 0x1A, 0x1A, 0x19, 0x19,
        0x18, 0x18, 0x17, 0x17, 0x16, 0x16, 0x15, 0x15,
        0x14, 0x14, 0x13, 0x13, 0x12, 0x12, 0x11, 0x11,
        0x10, 0x10
        };

        // no don't ask me WHY this table exsits!
        static readonly byte[] _unkTable2_2 = {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
        0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
        0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
        0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
        0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
        0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
        0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57,
        0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x6F,
        0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67,
        0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
        0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77,
        0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F
        };

        static readonly byte[] _unkTable2_3 = {
        0x40, 0x40, 0x40, 0x3F, 0x3F, 0x3F, 0x3E, 0x3E,
        0x3E, 0x3D, 0x3D, 0x3D, 0x3C, 0x3C, 0x3C, 0x3B,
        0x3B, 0x3B, 0x3A, 0x3A, 0x3A, 0x39, 0x39, 0x39,
        0x38, 0x38, 0x38, 0x37, 0x37, 0x37, 0x36, 0x36,
        0x36, 0x35, 0x35, 0x35, 0x34, 0x34, 0x34, 0x33,
        0x33, 0x33, 0x32, 0x32, 0x32, 0x31, 0x31, 0x31,
        0x30, 0x30, 0x30, 0x2F, 0x2F, 0x2F, 0x2E, 0x2E,
        0x2E, 0x2D, 0x2D, 0x2D, 0x2C, 0x2C, 0x2C, 0x2B,
        0x2B, 0x2B, 0x2A, 0x2A, 0x2A, 0x29, 0x29, 0x29,
        0x28, 0x28, 0x28, 0x27, 0x27, 0x27, 0x26, 0x26,
        0x26, 0x25, 0x25, 0x25, 0x24, 0x24, 0x24, 0x23,
        0x23, 0x23, 0x22, 0x22, 0x22, 0x21, 0x21, 0x21,
        0x20, 0x20, 0x20, 0x1F, 0x1F, 0x1F, 0x1E, 0x1E,
        0x1E, 0x1D, 0x1D, 0x1D, 0x1C, 0x1C, 0x1C, 0x1B,
        0x1B, 0x1B, 0x1A, 0x1A, 0x1A, 0x19, 0x19, 0x19,
        0x18, 0x18, 0x18, 0x17, 0x17, 0x17, 0x16, 0x16,
        0x16, 0x15
        };

        // This table is used to modify the frequency of the notes, depending on the
        // note value and unk16. In theory, we could very well try to access memory
        // outside this table, but in reality that probably won't happen.
        //
        // This could be some sort of pitch bend, but I have yet to see it used for
        // anything so it's hard to say.
        static readonly byte[][] _unkTables = {
        // 0
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x19,
            0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21 },
        // 1
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x04, 0x06, 0x07, 0x09,
            0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11,
            0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x1A,
            0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x22, 0x24 },
        // 2
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x04, 0x06, 0x08, 0x09,
            0x0A, 0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x12, 0x13,
            0x14, 0x15, 0x16, 0x17, 0x19, 0x1A, 0x1C, 0x1D,
            0x1E, 0x1F, 0x20, 0x21, 0x22, 0x24, 0x25, 0x26 },
        // 3
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x04, 0x06, 0x08, 0x0A,
            0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x12, 0x13,
            0x14, 0x15, 0x16, 0x17, 0x18, 0x1A, 0x1C, 0x1D,
            0x1E, 0x1F, 0x20, 0x21, 0x23, 0x25, 0x27, 0x28 },
        // 4
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x04, 0x06, 0x08, 0x0A,
            0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x13, 0x15,
            0x16, 0x17, 0x18, 0x19, 0x1B, 0x1D, 0x1F, 0x20,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x28, 0x2A },
        // 5
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x05, 0x07, 0x09, 0x0B,
            0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x13, 0x15,
            0x16, 0x17, 0x18, 0x19, 0x1B, 0x1D, 0x1F, 0x20,
            0x21, 0x22, 0x23, 0x25, 0x27, 0x29, 0x2B, 0x2D },
        // 6
        new byte[]
        { 0x00, 0x01, 0x02, 0x03, 0x05, 0x07, 0x09, 0x0B,
            0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x13, 0x15,
            0x16, 0x17, 0x18, 0x1A, 0x1C, 0x1E, 0x21, 0x24,
            0x25, 0x26, 0x27, 0x29, 0x2B, 0x2D, 0x2F, 0x30 },
        // 7
        new byte[]
        { 0x00, 0x01, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C,
            0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x13, 0x15, 0x18,
            0x19, 0x1A, 0x1C, 0x1D, 0x1F, 0x21, 0x23, 0x25,
            0x26, 0x27, 0x29, 0x2B, 0x2D, 0x2F, 0x30, 0x32 },
        // 8
        new byte[]
        { 0x00, 0x01, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0D,
            0x0E, 0x0F, 0x10, 0x11, 0x12, 0x14, 0x17, 0x1A,
            0x19, 0x1A, 0x1C, 0x1E, 0x20, 0x22, 0x25, 0x28,
            0x29, 0x2A, 0x2B, 0x2D, 0x2F, 0x31, 0x33, 0x35 },
        // 9
        new byte[]
        { 0x00, 0x01, 0x03, 0x05, 0x07, 0x09, 0x0B, 0x0E,
            0x0F, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1B,
            0x1C, 0x1D, 0x1E, 0x20, 0x22, 0x24, 0x26, 0x29,
            0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36, 0x39 },
        // 10
        new byte[]
        { 0x00, 0x01, 0x03, 0x05, 0x07, 0x09, 0x0B, 0x0E,
            0x0F, 0x10, 0x12, 0x14, 0x16, 0x19, 0x1B, 0x1E,
            0x1F, 0x21, 0x23, 0x25, 0x27, 0x29, 0x2B, 0x2D,
            0x2E, 0x2F, 0x31, 0x32, 0x34, 0x36, 0x39, 0x3C },
        // 11
        new byte[]
        { 0x00, 0x01, 0x03, 0x05, 0x07, 0x0A, 0x0C, 0x0F,
            0x10, 0x11, 0x13, 0x15, 0x17, 0x19, 0x1B, 0x1E,
            0x1F, 0x20, 0x22, 0x24, 0x26, 0x28, 0x2B, 0x2E,
            0x2F, 0x30, 0x32, 0x34, 0x36, 0x39, 0x3C, 0x3F },
        // 12
        new byte[]
        { 0x00, 0x02, 0x04, 0x06, 0x08, 0x0B, 0x0D, 0x10,
            0x11, 0x12, 0x14, 0x16, 0x18, 0x1B, 0x1E, 0x21,
            0x22, 0x23, 0x25, 0x27, 0x29, 0x2C, 0x2F, 0x32,
            0x33, 0x34, 0x36, 0x38, 0x3B, 0x34, 0x41, 0x44 },
        // 13
        new byte[]
        { 0x00, 0x02, 0x04, 0x06, 0x08, 0x0B, 0x0D, 0x11,
            0x12, 0x13, 0x15, 0x17, 0x1A, 0x1D, 0x20, 0x23,
            0x24, 0x25, 0x27, 0x29, 0x2C, 0x2F, 0x32, 0x35,
            0x36, 0x37, 0x39, 0x3B, 0x3E, 0x41, 0x44, 0x47 }
        };

        private delegate int OPCODE(ref int dataptr, Channel channel, byte value);

        private readonly OPCODE[] _parserOpcodeTable;

        byte[] _tablePtr1;
        byte[] _tablePtr2;

        public class Channel
        {
            public byte opExtraLevel2;
            public int dataptr;
            public byte duration;
            public byte repeatCounter;
            public sbyte baseOctave;
            public byte priority;
            public byte dataptrStackPos;
            public int[] dataptrStack = new int[4];
            public sbyte baseNote;
            public byte unk29;
            public byte unk31;
            public ushort unk30;
            public ushort unk37;
            public byte unk33;
            public byte unk34;
            public byte unk35;
            public byte unk36;
            public byte unk32;
            public byte unk41;
            public byte unk38;
            public byte opExtraLevel1;
            public byte spacing2;
            public byte baseFreq;
            public byte tempo;
            public byte position;
            public byte regAx;
            public byte regBx;
            public delegate void Callback(Channel c);
            public Callback primaryEffect;
            public Callback secondaryEffect;
            public byte fractionalSpacing;
            public byte opLevel1;
            public byte opLevel2;
            public byte opExtraLevel3;
            public byte twoChan;
            public byte unk39;
            public byte unk40;
            public byte spacing1;
            public byte durationRandomness;
            public byte unk19;
            public byte unk18;
            public sbyte unk20;
            public sbyte unk21;
            public byte unk22;
            public ushort offset;
            public byte tempoReset;
            public byte rawNote;
            public sbyte unk16;
        }

        public byte ADLVersion { get; set; }

        int _lastProcessed;
        sbyte _flagTrigger;
        int _curChannel;
        byte _soundTrigger;
        int _soundsPlaying;

        ushort _rnd;

        byte _unkValue1;
        byte _unkValue2;
        byte _unkValue3;
        byte _unkValue4;
        byte _unkValue5;
        byte _unkValue6;
        byte _unkValue7;
        byte _unkValue8;
        byte _unkValue9;
        byte _unkValue10;
        byte _unkValue11;
        byte _unkValue12;
        byte _unkValue13;
        byte _unkValue14;
        byte _unkValue15;
        byte _unkValue16;
        byte _unkValue17;
        byte _unkValue18;
        byte _unkValue19;
        byte _unkValue20;

        int _flags;

        byte[] _soundData;

        byte[] _soundIdTable = new byte[0x10];
        public Channel[] _channels = new Channel[10];

        byte _vibratoAndAMDepthBits;
        byte _rhythmSectionBits;

        byte _curRegOffset;
        byte _tempo;

        IOpl opl;

        public AdlibDriver(IOpl newopl)
        {
            opl = newopl;
            _rnd = 0x1234;
            _unkValue3 = 0xFF;

            for (int i = 0; i < _channels.Length; i++)
            {
                _channels[i] = new Channel();
                _channels[i].dataptr = -1;
            }

            _parserOpcodeTable = new OPCODE[] {
                // 0
                update_setRepeat,
                update_checkRepeat,
                update_setupProgram,
                update_setNoteSpacing,

                // 4
                update_jump,
                update_jumpToSubroutine,
                update_returnFromSubroutine,
                update_setBaseOctave,

                // 8
                update_stopChannel,
                update_playRest,
                update_writeAdlib,
                update_setupNoteAndDuration,

                // 12
                update_setBaseNote,
                update_setupSecondaryEffect1,
                update_stopOtherChannel,
                update_waitForEndOfProgram,

                // 16
                update_setupInstrument,
                update_setupPrimaryEffect1,
                update_removePrimaryEffect1,
                update_setBaseFreq,

                // 20
                update_stopChannel,
                update_setupPrimaryEffect2,
                update_stopChannel,
                update_stopChannel,

                // 24
                update_stopChannel,
                update_stopChannel,
                update_setPriority,
                update_stopChannel,

                // 28
                updateCallback23,
                updateCallback24,
                update_setExtraLevel1,
                update_stopChannel,

                // 32
                update_setupDuration,
                update_playNote,
                update_stopChannel,
                update_stopChannel,

                // 36
                update_setFractionalNoteSpacing,
                update_stopChannel,
                update_setTempo,
                update_removeSecondaryEffect1,

                // 40
                update_stopChannel,
                update_setChannelTempo,
                update_stopChannel,
                update_setExtraLevel3,

                // 44
                update_setExtraLevel2,
                update_changeExtraLevel2,
                update_setAMDepth,
                update_setVibratoDepth,

                // 48
                update_changeExtraLevel1,
                update_stopChannel,
                update_stopChannel,
                updateCallback38,

                // 52
                update_stopChannel,
                updateCallback39,
                update_removePrimaryEffect2,
                update_stopChannel,

                // 56
                update_stopChannel,
                updateCallback41,
                update_resetToGlobalTempo,
                update_nop1,

                // 60
                update_setDurationRandomness,
                update_changeChannelTempo,
                update_stopChannel,
                updateCallback46,

                // 64
                update_nop2,
                update_setupRhythmSection,
                update_playRhythmSection,
                update_removeRhythmSection,

                // 68
                updateCallback51,
                updateCallback52,
                updateCallback53,
                update_setSoundTrigger,

                // 72
                update_setTempoReset,
                updateCallback56,
                update_stopChannel
            };
        }

        // timer callback

        public void Callback()
        {
            // 	lock();
            --_flagTrigger;
            if (_flagTrigger < 0)
                _flags &= ~8;
            setupPrograms();
            executePrograms();

            byte temp = _unkValue3;
            _unkValue3 += _tempo;
            if (_unkValue3 < temp)
            {
                if ((--_unkValue2) == 0)
                {
                    _unkValue2 = _unkValue1;
                    ++_unkValue4;
                }
            }
            // 	unlock();
        }

        public int snd_initDriver()
        {
            _lastProcessed = _soundsPlaying = 0;
            resetAdlibState();
            return 0;
        }

        public int snd_setSoundData(byte[] data)
        {
            _soundData = data;
            return 0;
        }

        public int snd_startSong(int songId)
        {
            _flags |= 8;
            _flagTrigger = 1;

            var ptr = getProgram(songId);
            byte chan = ptr[0];

            if ((songId << 1) != 0)
            {
                if (chan == 9)
                {
                    if ((_flags & 2) != 0)
                        return 0;
                }
                else
                {
                    if ((_flags & 1) != 0)
                        return 0;
                }
            }

            _soundIdTable[_soundsPlaying++] = (byte)songId;
            _soundsPlaying &= 0x0F;

            return 0;
        }

        public int snd_unkOpcode3(int value)
        {
            int loop = value;
            if (value < 0)
            {
                value = 0;
                loop = 9;
            }
            loop -= value;
            ++loop;

            while ((loop--) != 0)
            {
                _curChannel = value;
                Channel channel = _channels[_curChannel];
                channel.priority = 0;
                channel.dataptr = -1;
                if (value != 9)
                {
                    noteOff(channel);
                }
                ++value;
            }

            return 0;
        }

        public int snd_readByte(int a, int b)
        {
            var ptr = getProgram(a).Slice(b);
            return ptr[0];
        }

        public int snd_writeByte(int a, int b, byte c)
        {
            var ptr = getProgram(a).Slice(b);
            byte oldValue = ptr[0];
            ptr[0] = (byte)c;
            return oldValue;
        }

        public int snd_setFlag(int flag)
        {
            int oldFlags = _flags;
            _flags |= flag;
            return oldFlags;
        }

        public int snd_clearFlag(int flag)
        {
            int oldFlags = _flags;
            _flags &= ~flag;
            return oldFlags;
        }

        private void resetAdlibState()
        {
            //   debugC(9, kDebugLevelSound, "resetAdlibState()");
            _rnd = 0x1234;

            // Authorize the control of the waveforms
            writeOPL(0x01, 0x20);

            // Select FM music mode
            writeOPL(0x08, 0x00);

            // I would guess the main purpose of this is to turn off the rhythm,
            // thus allowing us to use 9 melodic voices instead of 6.
            writeOPL(0xBD, 0x00);

            int loop = 10;
            while ((loop--) != 0)
            {
                if (loop != 9)
                {
                    // Silence the channel
                    writeOPL((byte)(0x40 + _regOffset[loop]), 0x3F);
                    writeOPL((byte)(0x43 + _regOffset[loop]), 0x3F);
                }
                initChannel(_channels[loop]);
            }
        }

        // A few words on opcode parsing and timing:
        //
        // First of all, We simulate a timer callback 72 times per second. Each timeout
        // we update each channel that has something to play.
        //
        // Each channel has its own individual tempo, which is added to its position.
        // This will frequently cause the position to "wrap around" but that is
        // intentional. In fact, it's the signal to go ahead and do more stuff with
        // that channel.
        //
        // Each channel also has a duration, indicating how much time is left on the
        // its current task. This duration is decreased by one. As long as it still has
        // not reached zero, the only thing that can happen is that the note is turned
        // off depending on manual or automatic note spacing. Once the duration reaches
        // zero, a new set of musical opcodes are executed.
        //
        // An opcode is one byte, followed by a variable number of parameters. Since
        // most opcodes have at least one one-byte parameter, we read that as well. Any
        // opcode that doesn't have that one parameter is responsible for moving the
        // data pointer back again.
        //
        // If the most significant bit of the opcode is 1, it's a function; call it.
        // The opcode functions return either 0 (continue), 1 (stop) or 2 (stop, and do
        // not run the effects callbacks).
        //
        // If the most significant bit of the opcode is 0, it's a note, and the first
        // parameter is its duration. (There are cases where the duration is modified
        // but that's an exception.) The note opcode is assumed to return 1, and is the
        // last opcode unless its duration is zero.
        //
        // Finally, most of the times that the callback is called, it will invoke the
        // effects callbacks. The final opcode in a set can prevent this, if it's a
        // function and it returns anything other than 1.

        private void executePrograms()
        {
            // Each channel runs its own program. There are ten channels: One for
            // each Adlib channel (0-8), plus one "control channel" (9) which is
            // the one that tells the other channels what to do. 

            for (_curChannel = 9; _curChannel >= 0; --_curChannel)
            {
                int result = 1;

                if (_channels[_curChannel].dataptr == -1)
                {
                    continue;
                }

                Channel channel = _channels[_curChannel];
                if (_curChannel != 9)
                {
                    _curRegOffset = _regOffset[_curChannel];
                }

                if (channel.tempoReset != 0)
                {
                    channel.tempo = _tempo;
                }

                byte backup = channel.position;
                channel.position += channel.tempo;
                if (channel.position < backup)
                {
                    if ((--channel.duration) != 0)
                    {
                        if (channel.duration == channel.spacing2)
                            noteOff(channel);
                        if (channel.duration == channel.spacing1 && _curChannel != 9)
                            noteOff(channel);
                    }
                    else
                    {
                        // An opcode is not allowed to modify its own
                        // data pointer except through the 'dataptr'
                        // parameter. To enforce that, we have to work
                        // on a copy of the data pointer.
                        //
                        // This fixes a subtle music bug where the
                        // wrong music would play when getting the
                        // quill in Kyra 1.
                        var dataptr = channel.dataptr;
                        while (dataptr != -1)
                        {
                            byte opcode = _soundData[dataptr]; dataptr++;
                            byte param = _soundData[dataptr]; dataptr++;

                            if ((opcode & 0x80) != 0)
                            {
                                opcode &= 0x7F;
                                if (opcode >= _parserOpcodeTable.Length)
                                    opcode = (byte)(_parserOpcodeTable.Length - 1);
                                // debugC(9, kDebugLevelSound, "Calling opcode '%s' (%d) (channel: %d)", _parserOpcodeTable[opcode].name, opcode, _curChannel);
                                result = _parserOpcodeTable[opcode](ref dataptr, channel, param);
                                channel.dataptr = dataptr;
                                if (result != 0)
                                    break;
                            }
                            else
                            {
                                // debugC(9, kDebugLevelSound, "Note on opcode 0x%02X (duration: %d) (channel: %d)", opcode, param, _curChannel);
                                setupNote(opcode, channel);
                                noteOn(channel);
                                setupDuration(param, channel);
                                if (param != 0)
                                {
                                    channel.dataptr = dataptr;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (result == 1)
                {
                    if (channel.primaryEffect != null)
                        channel.primaryEffect(channel);
                    if (channel.secondaryEffect != null)
                        channel.secondaryEffect(channel);
                }
            }
        }

        // I believe this is a random number generator. It actually does seem to
        // generate an even distribution of almost all numbers from 0 through 65535,
        // though in my tests some numbers were never generated.

        private ushort getRandomNr()
        {
            _rnd += 0x9248;
            ushort lowBits = (ushort)(_rnd & 7);
            _rnd >>= 3;
            _rnd = (ushort)(_rnd | (lowBits << 13));
            return _rnd;
        }

        private void setupDuration(byte duration, Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "setupDuration(%d, %lu)", duration, (long)(&channel - _channels));
            if (channel.durationRandomness != 0)
            {
                channel.duration = (byte)(duration + (getRandomNr() & channel.durationRandomness));
                return;
            }
            if (channel.fractionalSpacing != 0)
            {
                channel.spacing2 = (byte)((duration >> 3) * channel.fractionalSpacing);
            }
            channel.duration = duration;
        }

        // Apart from playing the note, this function also updates the variables for
        // primary effect 2.

        private void noteOn(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "noteOn(%lu)", (long)(&channel - _channels));

            // The "note on" bit is set, and the current note is played.

            channel.regBx |= 0x20;
            writeOPL((byte)(0xB0 + _curChannel), channel.regBx);

            sbyte shift = (sbyte)(9 - channel.unk33);
            ushort temp = (ushort)(channel.regAx | (channel.regBx << 8));
            channel.unk37 = (ushort)(((temp & 0x3FF) >> shift) & 0xFF);
            channel.unk38 = channel.unk36;
        }

        // This function may or may not play the note. It's usually followed by a call
        // to noteOn(), which will always play the current note.

        private void setupNote(byte rawNote, Channel channel, bool flag = false)
        {
            //   debugC(9, kDebugLevelSound, "setupNote(%d, %lu)", rawNote, (long)(&channel - _channels));

            channel.rawNote = rawNote;

            sbyte note = (sbyte)((rawNote & 0x0F) + channel.baseNote);
            sbyte octave = (sbyte)(((rawNote + channel.baseOctave) >> 4) & 0x0F);

            // There are only twelve notes. If we go outside that, we have to
            // adjust the note and octave.

            if (note >= 12)
            {
                note -= 12;
                octave++;
            }
            else if (note < 0)
            {
                note += 12;
                octave--;
            }

            // The calculation of frequency looks quite different from the original
            // disassembly at a first glance, but when you consider that the
            // largest possible value would be 0x0246 + 0xFF + 0x47 (and that's if
            // baseFreq is unsigned), freq is still a 10-bit value, just as it
            // should be to fit in the Ax and Bx registers.
            //
            // If it were larger than that, it could have overflowed into the
            // octave bits, and that could possibly have been used in some sound.
            // But as it is now, I can't see any way it would happen.

            ushort freq = (ushort)(_unkTable[note] + channel.baseFreq);

            // When called from callback 41, the behaviour is slightly different:
            // We adjust the frequency, even when channel.unk16 is 0.

            if (channel.unk16 != 0 || flag)
            {
                byte[] table;

                if (channel.unk16 >= 0)
                {
                    table = _unkTables[(channel.rawNote & 0x0F) + 2];
                    freq += table[channel.unk16];
                }
                else
                {
                    table = _unkTables[channel.rawNote & 0x0F];
                    freq -= table[-channel.unk16];
                }
            }

            channel.regAx = (byte)(freq & 0xFF);
            channel.regBx = (byte)((channel.regBx & 0x20) | (octave << 2) | ((freq >> 8) & 0x03));

            // Keep the note on or off
            writeOPL((byte)(0xA0 + _curChannel), channel.regAx);
            writeOPL((byte)(0xB0 + _curChannel), channel.regBx);
        }

        private void setupPrograms()
        {
            while (_lastProcessed != _soundsPlaying)
            {
                var ptr = getProgramOffset(_soundIdTable[_lastProcessed]);
                byte chan = _soundData[ptr++];
                byte priority = _soundData[ptr++];

                // Only start this sound if its priority is higher than the one
                // already playing.

                Channel channel = _channels[chan];

                if (priority >= channel.priority)
                {
                    initChannel(channel);
                    channel.priority = priority;
                    channel.dataptr = ptr;
                    channel.tempo = 0xFF;
                    channel.position = 0xFF;
                    channel.duration = 1;
                    unkOutput2(chan);
                }

                ++_lastProcessed;
                _lastProcessed &= 0x0F;
            }
        }

        private void unkOutput2(byte chan)
        {
            // debugC(9, kDebugLevelSound, "unkOutput2(%d)", chan);

            // The control channel has no corresponding Adlib channel

            if (chan >= 9)
                return;

            // I believe this has to do with channels 6, 7, and 8 being special
            // when Adlib's rhythm section is enabled.

            if (_rhythmSectionBits != 0 && chan >= 6)
                return;

            byte offset = _regOffset[chan];

            // The channel is cleared: First the attack/delay rate, then the
            // sustain level/release rate, and finally the note is turned off.

            writeOPL((byte)(0x60 + offset), 0xFF);
            writeOPL((byte)(0x63 + offset), 0xFF);

            writeOPL((byte)(0x80 + offset), 0xFF);
            writeOPL((byte)(0x83 + offset), 0xFF);

            writeOPL((byte)(0xB0 + chan), 0x00);

            // ...and then the note is turned on again, with whatever value is
            // still lurking in the A0 + chan register, but everything else -
            // including the two most significant frequency bit, and the octave -
            // set to zero.
            //
            // This is very strange behaviour, and causes problems with the ancient
            // FMOPL code we borrowed from AdPlug. I've added a workaround. See
            // fmopl.cpp for more details.
            //
            // More recent versions of the MAME FMOPL don't seem to have this
            // problem, but cannot currently be used because of licensing and
            // performance issues.
            //
            // Ken Silverman's Adlib emulator (which can be found on his Web page -
            // http://www.advsys.net/ken - and as part of AdPlug) also seems to be
            // immune, but is apparently not as feature complete as MAME's.

            writeOPL((byte)(0xB0 + chan), 0x20);
        }

        private void initChannel(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "initChannel(%lu)", (long)(&channel - _channels));
            //TODO: memset(&channel.dataptr, 0, sizeof(Channel) - ((char*)&channel.dataptr - (char*)&channel));

            channel.dataptr = -1;
            channel.tempo = 0xFF;
            channel.priority = 0;
            // normally here are nullfuncs but we set 0 for now
            channel.primaryEffect = null;
            channel.secondaryEffect = null;
            channel.spacing1 = 1;
        }

        private void noteOff(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "noteOff(%lu)", (long)(&channel - _channels));

            // The control channel has no corresponding Adlib channel

            if (_curChannel >= 9)
                return;

            // When the rhythm section is enabled, channels 6, 7 and 8 are special.

            if ((_rhythmSectionBits != 0) && _curChannel >= 6)
                return;

            // This means the "Key On" bit will always be 0
            channel.regBx &= 0xDF;

            // Octave / F-Number / Key-On
            writeOPL((byte)(0xB0 + _curChannel), channel.regBx);
        }

        // Old calling style: output0x388(0xABCD)
        // New calling style: writeOPL(0xAB, 0xCD)

        private void writeOPL(byte reg, byte val)
        {
            opl.WriteReg(reg, val);
        }

        // The sound data has at least two lookup tables:
        //
        // * One for programs, starting at offset 0.
        // * One for instruments, starting at offset depending on version.

        private Span<byte> getProgram(int progId)
        {
            var pos = READ_LE_ushort(CreateSpan(_soundData, 2 * progId));
            return CreateSpan(_soundData, pos);
        }

        private int getProgramOffset(int progId)
        {
            var pos = READ_LE_ushort(CreateSpan(_soundData, 2 * progId));
            return pos;
        }

        private static Span<byte> CreateSpan(byte[] data, int pos)
        {
            return new Span<byte>(data, pos, data.Length - pos);
        }

        private ushort READ_LE_ushort(int offset)
        {
            return (ushort)((_soundData[offset + 1] << 8) + _soundData[offset + 0]);
        }

        private ushort READ_BE_ushort(int offset)
        {
            return (ushort)((_soundData[offset + 0] << 8) + _soundData[offset + 1]);
        }

        private static ushort READ_LE_ushort(Span<byte> ptr)
        {
            return (ushort)((ptr[1] << 8) + ptr[0]);
        }

        private static ushort READ_BE_ushort(Span<byte> ptr)
        {
            return (ushort)((ptr[0] << 8) + ptr[1]);
        }

        // parser opcodes

        private int update_setRepeat(ref int dataptr, Channel channel, byte value)
        {
            channel.repeatCounter = value;
            return 0;
        }

        private int update_checkRepeat(ref int dataptr, Channel channel, byte value)
        {
            ++dataptr;
            if ((--channel.repeatCounter) != 0)
            {
                short add = (short)READ_LE_ushort(dataptr - 2);
                dataptr += add;
            }
            return 0;
        }

        private int update_setupProgram(ref int dataptr, Channel channel, byte value)
        {
            if (value == 0xFF)
                return 0;

            var ptr = getProgramOffset(value);
            byte chan = _soundData[ptr++];
            byte priority = _soundData[ptr++];

            Channel channel2 = _channels[chan];

            if (priority >= channel2.priority)
            {
                _flagTrigger = 1;
                _flags |= 8;
                initChannel(channel2);
                channel2.priority = priority;
                channel2.dataptr = ptr;
                channel2.tempo = 0xFF;
                channel2.position = 0xFF;
                channel2.duration = 1;
                unkOutput2(chan);
            }

            return 0;
        }

        private int update_setNoteSpacing(ref int dataptr, Channel channel, byte value)
        {
            channel.spacing1 = value;
            return 0;
        }

        private int update_jump(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            short add = (short)READ_LE_ushort(dataptr); dataptr += 2;
            dataptr += add;
            return 0;
        }

        private int update_jumpToSubroutine(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            short add = (short)READ_LE_ushort(dataptr); dataptr += 2;
            channel.dataptrStack[channel.dataptrStackPos++] = dataptr;
            dataptr += add;
            return 0;
        }

        private int update_returnFromSubroutine(ref int dataptr, Channel channel, byte value)
        {
            dataptr = channel.dataptrStack[--channel.dataptrStackPos];
            return 0;
        }

        private int update_setBaseOctave(ref int dataptr, Channel channel, byte value)
        {
            channel.baseOctave = (sbyte)value;
            return 0;
        }

        private int update_stopChannel(ref int dataptr, Channel channel, byte value)
        {
            channel.priority = 0;
            if (_curChannel != 9)
            {
                noteOff(channel);
            }
            dataptr = -1;
            return 2;
        }

        private int update_playRest(ref int dataptr, Channel channel, byte value)
        {
            setupDuration(value, channel);
            noteOff(channel);
            return (value != 0) ? 1 : 0;
        }

        private int update_writeAdlib(ref int dataptr, Channel channel, byte value)
        {
            writeOPL(value, _soundData[dataptr++]);
            return 0;
        }

        private int update_setupNoteAndDuration(ref int dataptr, Channel channel, byte value)
        {
            setupNote(value, channel);
            value = _soundData[dataptr++];
            setupDuration(value, channel);
            return (value != 0) ? 1 : 0;
        }

        private int update_setBaseNote(ref int dataptr, Channel channel, byte value)
        {
            channel.baseNote = (sbyte)value;
            return 0;
        }

        int update_setupSecondaryEffect1(ref int dataptr, Channel channel, byte value)
        {
            channel.unk18 = value;
            channel.unk19 = value;
            channel.unk20 = channel.unk21 = (sbyte)_soundData[dataptr++];
            channel.unk22 = _soundData[dataptr++];
            channel.offset = READ_LE_ushort(dataptr); dataptr += 2;
            channel.secondaryEffect = secondaryEffect1;
            return 0;
        }

        int updateCallback38(ref int dataptr, Channel channel, byte value)
        {
            int channelBackUp = _curChannel;

            _curChannel = value;
            Channel channel2 = _channels[value];
            channel2.duration = channel2.priority = 0;
            channel2.dataptr = -1;
            channel2.opExtraLevel2 = 0;

            if (value != 9)
            {
                byte outValue = _regOffset[value];

                // Feedback strength / Connection type
                writeOPL((byte)(0xC0 + _curChannel), 0x00);

                // Key scaling level / Operator output level
                writeOPL((byte)(0x43 + outValue), 0x3F);

                // Sustain Level / Release Rate
                writeOPL((byte)(0x83 + outValue), 0xFF);

                // Key On / Octave / Frequency
                writeOPL((byte)(0xB0 + _curChannel), 0x00);
            }

            _curChannel = channelBackUp;
            return 0;
        }

        int updateCallback39(ref int dataptr, Channel channel, byte value)
        {
            ushort unk = _soundData[dataptr++];
            unk = (ushort)(unk | value << 8);
            unk &= getRandomNr();

            ushort unk2 = (ushort)(((channel.regBx & 0x1F) << 8) | channel.regAx);
            unk2 += unk;
            unk2 = (ushort)(unk2 | ((channel.regBx & 0x20) << 8));

            // Frequency
            writeOPL((byte)(0xA0 + _curChannel), (byte)(unk2 & 0xFF));

            // Key On / Octave / Frequency
            writeOPL((byte)(0xB0 + _curChannel), (byte)((unk2 & 0xFF00) >> 8));

            return 0;
        }

        int update_removePrimaryEffect2(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            channel.primaryEffect = null;
            return 0;
        }

        int updateCallback41(ref int dataptr, Channel channel, byte value)
        {
            channel.unk16 = (sbyte)value;
            setupNote(channel.rawNote, channel, true);
            return 0;
        }

        int update_resetToGlobalTempo(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            channel.tempo = _tempo;
            return 0;
        }

        int update_nop1(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            return 0;
        }

        int update_setDurationRandomness(ref int dataptr, Channel channel, byte value)
        {
            channel.durationRandomness = value;
            return 0;
        }

        int update_changeChannelTempo(ref int dataptr, Channel channel, byte value)
        {
            int tempo = channel.tempo + (sbyte)value;

            if (tempo <= 0)
                tempo = 1;
            else if (tempo > 255)
                tempo = 255;

            channel.tempo = (byte)tempo;
            return 0;
        }

        int updateCallback46(ref int dataptr, Channel channel, byte value)
        {
            byte entry = _soundData[dataptr++];
            _tablePtr1 = _unkTable2[entry++];
            _tablePtr2 = _unkTable2[entry];
            if (value == 2)
            {
                // Frequency
                writeOPL(0xA0, (byte)(_tablePtr2[0]));
            }
            return 0;
        }

        // TODO: This is really the same as update_nop1(), so they should be combined
        //       into one single update_nop().

        int update_nop2(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            return 0;
        }

        int update_setupRhythmSection(ref int dataptr, Channel channel, byte value)
        {
            int channelBackUp = _curChannel;
            int regOffsetBackUp = _curRegOffset;

            _curChannel = 6;
            _curRegOffset = _regOffset[6];

            setupInstrument(_curRegOffset, getInstrument(value), channel);
            _unkValue6 = channel.opLevel2;

            _curChannel = 7;
            _curRegOffset = _regOffset[7];

            setupInstrument(_curRegOffset, getInstrument(_soundData[dataptr++]), channel);
            _unkValue7 = channel.opLevel1;
            _unkValue8 = channel.opLevel2;

            _curChannel = 8;
            _curRegOffset = _regOffset[8];

            setupInstrument(_curRegOffset, getInstrument(_soundData[dataptr++]), channel);
            _unkValue9 = channel.opLevel1;
            _unkValue10 = channel.opLevel2;

            // Octave / F-Number / Key-On for channels 6, 7 and 8

            _channels[6].regBx = (byte)(_soundData[dataptr++] & 0x2F);
            writeOPL(0xB6, _channels[6].regBx);
            writeOPL(0xA6, _soundData[dataptr++]);

            _channels[7].regBx = (byte)(_soundData[dataptr++] & 0x2F);
            writeOPL(0xB7, _channels[7].regBx);
            writeOPL(0xA7, _soundData[dataptr++]);

            _channels[8].regBx = (byte)(_soundData[dataptr++] & 0x2F);
            writeOPL(0xB8, _channels[8].regBx);
            writeOPL(0xA8, _soundData[dataptr++]);

            _rhythmSectionBits = 0x20;

            _curRegOffset = (byte)regOffsetBackUp;
            _curChannel = channelBackUp;
            return 0;
        }

        int update_playRhythmSection(ref int dataptr, Channel channel, byte value)
        {
            // Any instrument that we want to play, and which was already playing,
            // is temporarily keyed off. Instruments that were off already, or
            // which we don't want to play, retain their old on/off status. This is
            // probably so that the instrument's envelope is played from its
            // beginning again...

            writeOPL(0xBD, (byte)((_rhythmSectionBits & ~(value & 0x1F)) | 0x20));

            // ...but since we only set the rhythm instrument bits, and never clear
            // them (until the entire rhythm section is disabled), I'm not sure how
            // useful the cleverness above is. We could perhaps simply turn off all
            // the rhythm instruments instead.

            _rhythmSectionBits |= value;

            writeOPL(0xBD, (byte)(_vibratoAndAMDepthBits | 0x20 | _rhythmSectionBits));
            return 0;
        }

        int update_removeRhythmSection(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            _rhythmSectionBits = 0;

            // All the rhythm bits are cleared. The AM and Vibrato depth bits
            // remain unchanged.

            writeOPL(0xBD, _vibratoAndAMDepthBits);
            return 0;
        }

        int updateCallback51(ref int dataptr, Channel channel, byte value)
        {
            byte value2 = _soundData[dataptr++];

            if ((value & 1) != 0)
            {
                _unkValue12 = value2;

                // Channel 7, op1: Level Key Scaling / Total Level
                writeOPL(0x51, (byte)(checkValue((short)(value2 + _unkValue7 + _unkValue11 + _unkValue12))));
            }

            if ((value & 2) != 0)
            {
                _unkValue14 = value2;

                // Channel 8, op2: Level Key Scaling / Total Level
                writeOPL(0x55, (byte)(checkValue((short)(value2 + _unkValue10 + _unkValue13 + _unkValue14))));
            }

            if ((value & 4) != 0)
            {
                _unkValue15 = value2;

                // Channel 8, op1: Level Key Scaling / Total Level
                writeOPL(0x52, (byte)(checkValue((short)(value2 + _unkValue9 + _unkValue16 + _unkValue15))));
            }

            if ((value & 8) != 0)
            {
                _unkValue18 = value2;

                // Channel 7, op2: Level Key Scaling / Total Level
                writeOPL(0x54, (byte)(checkValue((short)(value2 + _unkValue8 + _unkValue17 + _unkValue18))));
            }

            if ((value & 16) != 0)
            {
                _unkValue20 = value2;

                // Channel 6, op2: Level Key Scaling / Total Level
                writeOPL(0x53, (byte)(checkValue((short)(value2 + _unkValue6 + _unkValue19 + _unkValue20))));
            }

            return 0;
        }

        int updateCallback52(ref int dataptr, Channel channel, byte value)
        {
            byte value2 = _soundData[dataptr++];

            if ((value & 1) != 0)
            {
                _unkValue11 = (byte)checkValue((short)(value2 + _unkValue7 + _unkValue11 + _unkValue12));

                // Channel 7, op1: Level Key Scaling / Total Level
                writeOPL(0x51, _unkValue11);
            }

            if ((value & 2) != 0)
            {
                _unkValue13 = (byte)checkValue((short)(value2 + _unkValue10 + _unkValue13 + _unkValue14));

                // Channel 8, op2: Level Key Scaling / Total Level
                writeOPL(0x55, _unkValue13);
            }

            if ((value & 4) != 0)
            {
                _unkValue16 = (byte)checkValue((short)(value2 + _unkValue9 + _unkValue16 + _unkValue15));

                // Channel 8, op1: Level Key Scaling / Total Level
                writeOPL(0x52, _unkValue16);
            }

            if ((value & 8) != 0)
            {
                _unkValue17 = (byte)checkValue((short)(value2 + _unkValue8 + _unkValue17 + _unkValue18));

                // Channel 7, op2: Level Key Scaling / Total Level
                writeOPL(0x54, _unkValue17);
            }

            if ((value & 16) != 0)
            {
                _unkValue19 = (byte)checkValue((short)(value2 + _unkValue6 + _unkValue19 + _unkValue20));

                // Channel 6, op2: Level Key Scaling / Total Level
                writeOPL(0x53, _unkValue19);
            }

            return 0;
        }

        int updateCallback53(ref int dataptr, Channel channel, byte value)
        {
            byte value2 = _soundData[dataptr++];

            if ((value & 1) != 0)
            {
                _unkValue11 = value2;

                // Channel 7, op1: Level Key Scaling / Total Level
                writeOPL(0x51, (byte)checkValue((short)(value2 + _unkValue7 + _unkValue12)));
            }

            if ((value & 2) != 0)
            {
                _unkValue13 = value2;

                // Channel 8, op2: Level Key Scaling / Total Level
                writeOPL(0x55, (byte)checkValue((short)(value2 + _unkValue10 + _unkValue14)));
            }

            if ((value & 4) != 0)
            {
                _unkValue16 = value2;

                // Channel 8, op1: Level Key Scaling / Total Level
                writeOPL(0x52, (byte)checkValue((short)(value2 + _unkValue9 + _unkValue15)));
            }

            if ((value & 8) != 0)
            {
                _unkValue17 = value2;

                // Channel 7, op2: Level Key Scaling / Total Level
                writeOPL(0x54, (byte)checkValue((short)(value2 + _unkValue8 + _unkValue18)));
            }

            if ((value & 16) != 0)
            {
                _unkValue19 = value2;

                // Channel 6, op2: Level Key Scaling / Total Level
                writeOPL(0x53, (byte)checkValue((short)(value2 + _unkValue6 + _unkValue20)));
            }

            return 0;
        }

        int update_setSoundTrigger(ref int dataptr, Channel channel, byte value)
        {
            _soundTrigger = value;
            return 0;
        }

        int update_setTempoReset(ref int dataptr, Channel channel, byte value)
        {
            channel.tempoReset = value;
            return 0;
        }

        int updateCallback56(ref int dataptr, Channel channel, byte value)
        {
            channel.unk39 = value;
            channel.unk40 = _soundData[dataptr++];
            return 0;
        }

        void primaryEffect1(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "Calling primaryEffect1 (channel: %d)", _curChannel);
            byte temp = channel.unk31;
            channel.unk31 += channel.unk29;
            if (channel.unk31 >= temp)
                return;

            // Initialise unk1 to the current frequency
            ushort unk1 = (ushort)(((channel.regBx & 3) << 8) | channel.regAx);

            // This is presumably to shift the "note on" bit so far to the left
            // that it won't be affected by any of the calculations below.
            ushort unk2 = (ushort)(((channel.regBx & 0x20) << 8) | (channel.regBx & 0x1C));

            short unk3 = (short)channel.unk30;

            if (unk3 >= 0)
            {
                unk1 = (ushort)(unk1 + unk3);
                if (unk1 >= 734)
                {
                    // The new frequency is too high. Shift it down and go
                    // up one octave.
                    unk1 >>= 1;
                    if ((unk1 & 0x3FF) == 0)
                        ++unk1;
                    unk2 = (ushort)((unk2 & 0xFF00) | ((unk2 + 4) & 0xFF));
                    unk2 &= 0xFF1C;
                }
            }
            else
            {
                unk1 = (ushort)(unk1 + unk3);
                if (unk1 < 388)
                {
                    // The new frequency is too low. Shift it up and go
                    // down one octave.
                    unk1 <<= 1;
                    if ((unk1 & 0x3FF) == 0)
                        --unk1;
                    unk2 = (ushort)((unk2 & 0xFF00) | ((unk2 - 4) & 0xFF));
                    unk2 &= 0xFF1C;
                }
            }

            // Make sure that the new frequency is still a 10-bit value.
            unk1 &= 0x3FF;

            writeOPL((byte)(0xA0 + _curChannel), (byte)(unk1 & 0xFF));
            channel.regAx = (byte)(unk1 & 0xFF);

            // Shift down the "note on" bit again.
            byte value = (byte)(unk1 >> 8);
            value = (byte)(value | (unk2 >> 8) & 0xFF);
            value = (byte)(value | unk2 & 0xFF);

            writeOPL((byte)(0xB0 + _curChannel), value);
            channel.regBx = value;
        }

        void primaryEffect2(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "Calling primaryEffect2 (channel: %d)", _curChannel);
            if (channel.unk38 != 0)
            {
                --channel.unk38;
                return;
            }

            byte temp = channel.unk41;
            channel.unk41 += channel.unk32;
            if (channel.unk41 < temp)
            {
                ushort unk1 = channel.unk37;
                if ((--channel.unk34) == 0)
                {
                    unk1 ^= 0xFFFF;
                    ++unk1;
                    channel.unk37 = unk1;
                    channel.unk34 = channel.unk35;
                }

                ushort unk2 = (ushort)((channel.regAx | (channel.regBx << 8)) & 0x3FF);
                unk2 += unk1;

                channel.regAx = (byte)(unk2 & 0xFF);
                channel.regBx = (byte)((channel.regBx & 0xFC) | (unk2 >> 8));

                // Octave / F-Number / Key-On
                writeOPL((byte)(0xA0 + _curChannel), channel.regAx);
                writeOPL((byte)(0xB0 + _curChannel), channel.regBx);
            }
        }

        void secondaryEffect1(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "Calling secondaryEffect1 (channel: %d)", _curChannel);
            byte temp = channel.unk18;
            channel.unk18 += channel.unk19;
            if (channel.unk18 < temp)
            {
                if (--channel.unk21 < 0)
                {
                    channel.unk21 = channel.unk20;
                }
                writeOPL((byte)(channel.unk22 + _curRegOffset), _soundData[channel.offset + channel.unk21]);
            }
        }

        int update_stopOtherChannel(ref int dataptr, Channel channel, byte value)
        {
            Channel channel2 = _channels[value];
            channel2.duration = 0;
            channel2.priority = 0;
            channel2.dataptr = -1;
            return 0;
        }

        int update_waitForEndOfProgram(ref int dataptr, Channel channel, byte value)
        {
            var ptr = getProgramOffset(value);
            byte chan = _soundData[ptr];

            if (_channels[chan].dataptr == -1)
            {
                return 0;
            }

            dataptr -= 2;
            return 2;
        }

        int update_setupInstrument(ref int dataptr, Channel channel, byte value)
        {
            setupInstrument(_curRegOffset, getInstrument(value), channel);
            return 0;
        }

        int update_setupPrimaryEffect1(ref int dataptr, Channel channel, byte value)
        {
            channel.unk29 = value;
            channel.unk30 = READ_BE_ushort(dataptr);
            dataptr += 2;
            channel.primaryEffect = primaryEffect1;
            channel.unk31 = 0xFF;
            return 0;
        }

        int update_removePrimaryEffect1(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            channel.primaryEffect = null;
            channel.unk30 = 0;
            return 0;
        }

        int update_setBaseFreq(ref int dataptr, Channel channel, byte value)
        {
            channel.baseFreq = value;
            return 0;
        }

        int update_setupPrimaryEffect2(ref int dataptr, Channel channel, byte value)
        {
            channel.unk32 = value;
            channel.unk33 = _soundData[dataptr++];
            byte temp = _soundData[dataptr++];
            channel.unk34 = (byte)(temp + 1);
            channel.unk35 = (byte)(temp << 1);
            channel.unk36 = _soundData[dataptr++];
            channel.primaryEffect = primaryEffect2;
            return 0;
        }

        int update_setPriority(ref int dataptr, Channel channel, byte value)
        {
            channel.priority = value;
            return 0;
        }

        int updateCallback23(ref int dataptr, Channel channel, byte value)
        {
            value >>= 1;
            _unkValue1 = _unkValue2 = value;
            _unkValue3 = 0xFF;
            _unkValue4 = _unkValue5 = 0;
            return 0;
        }

        int updateCallback24(ref int dataptr, Channel channel, byte value)
        {
            if (_unkValue5 != 0)
            {
                if ((_unkValue4 & value) != 0)
                {
                    _unkValue5 = 0;
                    return 0;
                }
            }

            if ((value & _unkValue4) == 0)
            {
                ++_unkValue5;
            }

            dataptr -= 2;
            channel.duration = 1;
            return 2;
        }

        int update_setExtraLevel1(ref int dataptr, Channel channel, byte value)
        {
            channel.opExtraLevel1 = value;
            adjustVolume(channel);
            return 0;
        }

        int update_setupDuration(ref int dataptr, Channel channel, byte value)
        {
            setupDuration(value, channel);
            return (value != 0) ? 1 : 0;
        }

        int update_playNote(ref int dataptr, Channel channel, byte value)
        {
            setupDuration(value, channel);
            noteOn(channel);
            return (value != 0) ? 1 : 0;
        }

        int update_setFractionalNoteSpacing(ref int dataptr, Channel channel, byte value)
        {
            channel.fractionalSpacing = (byte)(value & 7);
            return 0;
        }

        int update_setTempo(ref int dataptr, Channel channel, byte value)
        {
            _tempo = value;
            return 0;
        }

        int update_removeSecondaryEffect1(ref int dataptr, Channel channel, byte value)
        {
            --dataptr;
            channel.secondaryEffect = null;
            return 0;
        }

        int update_setChannelTempo(ref int dataptr, Channel channel, byte value)
        {
            channel.tempo = value;
            return 0;
        }

        int update_setExtraLevel3(ref int dataptr, Channel channel, byte value)
        {
            channel.opExtraLevel3 = value;
            return 0;
        }

        int update_setExtraLevel2(ref int dataptr, Channel channel, byte value)
        {
            int channelBackUp = _curChannel;

            _curChannel = value;
            Channel channel2 = _channels[value];
            channel2.opExtraLevel2 = _soundData[dataptr++];
            adjustVolume(channel2);

            _curChannel = channelBackUp;
            return 0;
        }

        int update_changeExtraLevel2(ref int dataptr, Channel channel, byte value)
        {
            int channelBackUp = _curChannel;

            _curChannel = value;
            Channel channel2 = _channels[value];
            channel2.opExtraLevel2 += _soundData[dataptr++];
            adjustVolume(channel2);

            _curChannel = channelBackUp;
            return 0;
        }

        // Apart from initialising to zero, these two functions are the only ones that
        // modify _vibratoAndAMDepthBits.

        int update_setAMDepth(ref int dataptr, Channel channel, byte value)
        {
            if ((value & 1) != 0)
                _vibratoAndAMDepthBits |= 0x80;
            else
                _vibratoAndAMDepthBits &= 0x7F;

            writeOPL(0xBD, _vibratoAndAMDepthBits);
            return 0;
        }

        int update_setVibratoDepth(ref int dataptr, Channel channel, byte value)
        {
            if ((value & 1) != 0)
                _vibratoAndAMDepthBits |= 0x40;
            else
                _vibratoAndAMDepthBits &= 0xBF;

            writeOPL(0xBD, _vibratoAndAMDepthBits);
            return 0;
        }

        int update_changeExtraLevel1(ref int dataptr, Channel channel, byte value)
        {
            channel.opExtraLevel1 += value;
            adjustVolume(channel);
            return 0;
        }

        void adjustVolume(Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "adjustVolume(%lu)", (long)(&channel - _channels));
            // Level Key Scaling / Total Level

            writeOPL((byte)(0x43 + _regOffset[_curChannel]), calculateOpLevel2(channel));
            if (channel.twoChan != 0)
                writeOPL((byte)(0x40 + _regOffset[_curChannel]), calculateOpLevel1(channel));
        }

        ushort checkValue(short val)
        {
            if (val < 0)
                val = 0;
            else if (val > 0x3F)
                val = 0x3F;
            return (ushort)val;
        }

        byte calculateOpLevel1(Channel channel)
        {
            sbyte value = (sbyte)(channel.opLevel1 & 0x3F);

            if (channel.twoChan != 0)
            {
                value = (sbyte)(value + channel.opExtraLevel1);
                value = (sbyte)(value + channel.opExtraLevel2);
                value = (sbyte)(value + channel.opExtraLevel3);
            }

            // Preserve the scaling level bits from opLevel1
            return (byte)(checkValue(value) | (channel.opLevel1 & 0xC0));
        }

        byte calculateOpLevel2(Channel channel)
        {
            sbyte value = (sbyte)(channel.opLevel2 & 0x3F);

            value = (sbyte)(value + channel.opExtraLevel1);
            value = (sbyte)(value + channel.opExtraLevel2);
            value = (sbyte)(value + channel.opExtraLevel3);

            // Preserve the scaling level bits from opLevel2

            return (byte)(checkValue(value) | (channel.opLevel2 & 0xC0));
        }

        void setupInstrument(byte regOffset, int dataptr, Channel channel)
        {
            //   debugC(9, kDebugLevelSound, "setupInstrument(%d, %p, %lu)", regOffset, (const void *)dataptr, (long)(&channel - _channels));
            // Amplitude Modulation / Vibrato / Envelope Generator Type /
            // Keyboard Scaling Rate / Modulator Frequency Multiple
            writeOPL((byte)(0x20 + regOffset), _soundData[dataptr++]);
            writeOPL((byte)(0x23 + regOffset), _soundData[dataptr++]);

            byte temp = _soundData[dataptr++];

            // Feedback / Algorithm

            // It is very likely that _curChannel really does refer to the same
            // channel as regOffset, but there's only one Cx register per channel.

            writeOPL((byte)(0xC0 + _curChannel), temp);

            // The algorithm bit. I don't pretend to understand this fully, but
            // "If set to 0, operator 1 modulates operator 2. In this case,
            // operator 2 is the only one producing sound. If set to 1, both
            // operators produce sound directly. Complex sounds are more easily
            // created if the algorithm is set to 0."

            channel.twoChan = (byte)(temp & 1);

            // Waveform Select
            writeOPL((byte)(0xE0 + regOffset), _soundData[dataptr++]);
            writeOPL((byte)(0xE3 + regOffset), _soundData[dataptr++]);

            channel.opLevel1 = _soundData[dataptr++];
            channel.opLevel2 = _soundData[dataptr++];

            // Level Key Scaling / Total Level
            writeOPL((byte)(0x40 + regOffset), calculateOpLevel1(channel));
            writeOPL((byte)(0x43 + regOffset), calculateOpLevel2(channel));

            // Attack Rate / Decay Rate
            writeOPL((byte)(0x60 + regOffset), _soundData[dataptr++]);
            writeOPL((byte)(0x63 + regOffset), _soundData[dataptr++]);

            // Sustain Level / Release Rate
            writeOPL((byte)(0x80 + regOffset), _soundData[dataptr++]);
            writeOPL((byte)(0x83 + regOffset), _soundData[dataptr++]);
        }

        int getInstrument(int instrumentId)
        {
            ushort instOffset = 0;
            switch (ADLVersion)
            {
                case 1:
                    instOffset = 150 * 2;
                    break;
                case 2:
                    instOffset = 250 * 2;
                    break;
                case 3:
                    instOffset = 500 * 2;
                    break;
            }
            return READ_LE_ushort(instOffset + 2 * instrumentId);
        }
    }
}
