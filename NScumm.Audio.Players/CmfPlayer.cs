//
//  MidPlayer.cs
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
    internal sealed class CmfPlayer : IMusicPlayer
    {
        struct CMFHEADER
        {
            public ushort iInstrumentBlockOffset;
            public ushort iMusicOffset;
            public ushort iTicksPerQuarterNote;
            public ushort iTicksPerSecond;
            public ushort iTagOffsetTitle;
            public ushort iTagOffsetComposer;
            public ushort iTagOffsetRemarks;
            public byte[] iChannelsInUse;
            public ushort iNumInstruments;
            public ushort iTempo;
        }

        struct OPERATOR
        {
            public byte iCharMult;
            public byte iScalingOutput;
            public byte iAttackDecay;
            public byte iSustainRelease;
            public byte iWaveSel;
        }

        struct SBI
        {
            public OPERATOR[] op; // 0 == modulator, 1 == carrier
            public byte iConnection;
        }

        struct MIDICHANNEL
        {
            public int iPatch; // MIDI patch for this channel
            public int iPitchbend; // Current pitchbend amount for this channel
            public int iTranspose; // Transpose amount for this channel (between -128 and +128)
        }

        struct OPLCHANNEL
        {
            public int iNoteStart;   // When the note started playing (longest notes get cut first, 0 == channel free)
            public int iMIDINote;    // MIDI note number currently being played on this OPL channel
            public int iMIDIChannel; // Source MIDI channel where this note came from
            public int iMIDIPatch;   // Current MIDI patch set on this OPL channel
        }

        byte[] data; // song data (CMF music block)
        int iPlayPointer;   // Current location of playback pointer
        int iSongLen;       // Max value for iPlayPointer
        CMFHEADER cmfHeader;
        SBI[] pInstruments;
        bool bPercussive; // are rhythm-mode instruments enabled?
        byte[] iCurrentRegs = new byte[256]; // Current values in the OPL chip
        byte iPrevCommand; // Previous command (used for repeated MIDI commands, as the seek and playback code need to share this)
        byte[] iNotePlaying = new byte[16]; // Last note turned on, used for duplicate note check
        bool[] bNoteFix = new bool[16]; // Fix duplicated Note On / Note Off

        int iNoteCount;  // Used to count how long notes have been playing for
        MIDICHANNEL[] chMIDI = new MIDICHANNEL[16];
        OPLCHANNEL[] chOPL = new OPLCHANNEL[9];

        // Additions for AdPlug's design
        int iDelayRemaining;
        bool bSongEnd;
        string strTitle, strComposer, strRemarks;

        // OPL register offsets
        const int BASE_CHAR_MULT = 0x20;
        const int BASE_SCAL_LEVL = 0x40;
        const int BASE_ATCK_DCAY = 0x60;
        const int BASE_SUST_RLSE = 0x80;
        const int BASE_FNUM_L = 0xA0;
        const int BASE_KEYON_FREQ = 0xB0;
        const int BASE_RHYTHM = 0xBD;
        const int BASE_WAVE = 0xE0;
        const int BASE_FEED_CONN = 0xC0;

        const int OPLBIT_KEYON = 0x20; // Bit in BASE_KEYON_FREQ register for turning a note on

        // Supplied with a channel, return the offset from a base OPL register for the
        // Modulator cell (e.g. channel 4's modulator is at offset 0x09.  Since 0x60 is
        // the attack/decay function, register 0x69 will thus set the attack/decay for
        // channel 4's modulator.)  (channels go from 0 to 8 inclusive)
        static int OPLOFFSET(int channel) => (((channel) / 3) * 8 + ((channel) % 3));

        // These 16 instruments are repeated to fill up the 128 available slots.  A CMF
        // file can override none/some/all of the 128 slots with custom instruments,
        // so any that aren't overridden are still available for use with these default
        // patches.  The Word Rescue CMFs are good examples of songs that rely on these
        // default patches.
        byte[] cDefaultPatches =
            {0x01,0x11,0x4F,0x00,0xF1,0xD2,0x53,0x74,0x00,0x00,0x06
            ,0x07,0x12,0x4F,0x00,0xF2,0xF2,0x60,0x72,0x00,0x00,0x08
            ,0x31,0xA1,0x1C,0x80,0x51,0x54,0x03,0x67,0x00,0x00,0x0E
            ,0x31,0xA1,0x1C,0x80,0x41,0x92,0x0B,0x3B,0x00,0x00,0x0E
            ,0x31,0x16,0x87,0x80,0xA1,0x7D,0x11,0x43,0x00,0x00,0x08
            ,0x30,0xB1,0xC8,0x80,0xD5,0x61,0x19,0x1B,0x00,0x00,0x0C
            ,0xF1,0x21,0x01,0x00,0x97,0xF1,0x17,0x18,0x00,0x00,0x08
            ,0x32,0x16,0x87,0x80,0xA1,0x7D,0x10,0x33,0x00,0x00,0x08
            ,0x01,0x12,0x4F,0x00,0x71,0x52,0x53,0x7C,0x00,0x00,0x0A
            ,0x02,0x03,0x8D,0x00,0xD7,0xF5,0x37,0x18,0x00,0x00,0x04
            ,0x21,0x21,0xD1,0x00,0xA3,0xA4,0x46,0x25,0x00,0x00,0x0A
            ,0x22,0x22,0x0F,0x00,0xF6,0xF6,0x95,0x36,0x00,0x00,0x0A
            ,0xE1,0xE1,0x00,0x00,0x44,0x54,0x24,0x34,0x02,0x02,0x07
            ,0xA5,0xB1,0xD2,0x80,0x81,0xF1,0x03,0x05,0x00,0x00,0x02
            ,0x71,0x22,0xC5,0x00,0x6E,0x8B,0x17,0x0E,0x00,0x00,0x02
            ,0x32,0x21,0x16,0x80,0x73,0x75,0x24,0x57,0x00,0x00,0x0E
        };

        public IOpl Opl { get; }

        public float RefreshRate
        {
            get
            {
                if (iDelayRemaining != 0)
                    return (float)cmfHeader.iTicksPerSecond / (float)iDelayRemaining;
                // Delay-remaining is zero (e.g. start of song) so use a tiny delay
                return cmfHeader.iTicksPerSecond; // wait for one tick
            }
        }

        public CmfPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
        }

        public bool Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var br = new BinaryReader(fs);
                var cSig = new string(br.ReadChars(4));
                if (cSig != "CTMF")
                {
                    // Not a CMF file
                    return false;
                }

                ushort iVer = br.ReadUInt16();
                if ((iVer != 0x0101) && (iVer != 0x0100))
                {
                    Console.Error.WriteLine($"CMF file is not v1.0 or v1.1 (reports {iVer >> 8}.{iVer & 0xFF})");
                    return false;
                }

                cmfHeader.iInstrumentBlockOffset = br.ReadUInt16();
                cmfHeader.iMusicOffset = br.ReadUInt16();
                cmfHeader.iTicksPerQuarterNote = br.ReadUInt16();
                cmfHeader.iTicksPerSecond = br.ReadUInt16();
                cmfHeader.iTagOffsetTitle = br.ReadUInt16();
                cmfHeader.iTagOffsetComposer = br.ReadUInt16();
                cmfHeader.iTagOffsetRemarks = br.ReadUInt16();

                // This checks will fix crash for a lot of broken files
                // Title, Composer and Remarks blocks usually located before Instrument block
                // But if not this will indicate invalid offset value (sometimes even bigger than filesize)
                if (cmfHeader.iTagOffsetTitle >= cmfHeader.iInstrumentBlockOffset)
                    cmfHeader.iTagOffsetTitle = 0;
                if (cmfHeader.iTagOffsetComposer >= cmfHeader.iInstrumentBlockOffset)
                    cmfHeader.iTagOffsetComposer = 0;
                if (cmfHeader.iTagOffsetRemarks >= cmfHeader.iInstrumentBlockOffset)
                    cmfHeader.iTagOffsetRemarks = 0;

                cmfHeader.iChannelsInUse = br.ReadBytes(16);
                if (iVer == 0x0100)
                {
                    cmfHeader.iNumInstruments = br.ReadByte();
                    cmfHeader.iTempo = 0;
                }
                else
                { // 0x0101
                    cmfHeader.iNumInstruments = br.ReadUInt16();
                    cmfHeader.iTempo = br.ReadUInt16();
                }

                // Load the instruments

                fs.Seek(cmfHeader.iInstrumentBlockOffset, SeekOrigin.Begin);
                pInstruments = new SBI[
                  (cmfHeader.iNumInstruments < 128) ? 128 : cmfHeader.iNumInstruments
                ];  // Always at least 128 available for use

                for (int i = 0; i < cmfHeader.iNumInstruments; i++)
                {
                    pInstruments[i].op = new OPERATOR[2];
                    pInstruments[i].op[0].iCharMult = br.ReadByte();
                    pInstruments[i].op[1].iCharMult = br.ReadByte();
                    pInstruments[i].op[0].iScalingOutput = br.ReadByte();
                    pInstruments[i].op[1].iScalingOutput = br.ReadByte();
                    pInstruments[i].op[0].iAttackDecay = br.ReadByte();
                    pInstruments[i].op[1].iAttackDecay = br.ReadByte();
                    pInstruments[i].op[0].iSustainRelease = br.ReadByte();
                    pInstruments[i].op[1].iSustainRelease = br.ReadByte();
                    pInstruments[i].op[0].iWaveSel = br.ReadByte();
                    pInstruments[i].op[1].iWaveSel = br.ReadByte();
                    pInstruments[i].iConnection = br.ReadByte();
                    fs.Seek(5, SeekOrigin.Current);  // skip over the padding bytes
                }

                // Set the rest of the instruments to the CMF defaults
                for (int i = cmfHeader.iNumInstruments; i < 128; i++)
                {
                    pInstruments[i].op = new OPERATOR[2];
                    pInstruments[i].op[0].iCharMult = cDefaultPatches[(i % 16) * 11 + 0];
                    pInstruments[i].op[1].iCharMult = cDefaultPatches[(i % 16) * 11 + 1];
                    pInstruments[i].op[0].iScalingOutput = cDefaultPatches[(i % 16) * 11 + 2];
                    pInstruments[i].op[1].iScalingOutput = cDefaultPatches[(i % 16) * 11 + 3];
                    pInstruments[i].op[0].iAttackDecay = cDefaultPatches[(i % 16) * 11 + 4];
                    pInstruments[i].op[1].iAttackDecay = cDefaultPatches[(i % 16) * 11 + 5];
                    pInstruments[i].op[0].iSustainRelease = cDefaultPatches[(i % 16) * 11 + 6];
                    pInstruments[i].op[1].iSustainRelease = cDefaultPatches[(i % 16) * 11 + 7];
                    pInstruments[i].op[0].iWaveSel = cDefaultPatches[(i % 16) * 11 + 8];
                    pInstruments[i].op[1].iWaveSel = cDefaultPatches[(i % 16) * 11 + 9];
                    pInstruments[i].iConnection = cDefaultPatches[(i % 16) * 11 + 10];
                }

                if (cmfHeader.iTagOffsetTitle != 0)
                {
                    fs.Seek(cmfHeader.iTagOffsetTitle, SeekOrigin.Begin);
                    strTitle = ReadString(br);
                }
                if (cmfHeader.iTagOffsetComposer != 0)
                {
                    fs.Seek(cmfHeader.iTagOffsetComposer, SeekOrigin.Begin);
                    strComposer = ReadString(br);
                }
                if (cmfHeader.iTagOffsetRemarks != 0)
                {
                    fs.Seek(cmfHeader.iTagOffsetRemarks, SeekOrigin.Begin);
                    strRemarks = ReadString(br);
                }

                // Load the MIDI data into memory
                fs.Seek(cmfHeader.iMusicOffset, SeekOrigin.Begin);
                iSongLen = (int)(fs.Length - cmfHeader.iMusicOffset);
                data = br.ReadBytes(iSongLen);

                Rewind(0);

                return true;
            }
        }

        public bool Update()
        {
            // This has to be here and not in getrefresh() for some reason.
            iDelayRemaining = 0;

            // Read in the next event
            while (iDelayRemaining == 0)
            {
                byte iCommand = data[iPlayPointer++];
                if ((iCommand & 0x80) == 0)
                {
                    // Running status, use previous command
                    iPlayPointer--;
                    iCommand = iPrevCommand;
                }
                else
                {
                    iPrevCommand = iCommand;
                }
                byte iChannel = (byte)(iCommand & 0x0F);
                switch (iCommand & 0xF0)
                {
                    case 0x80:
                        { // Note off (two data bytes)
                            byte iNote = data[iPlayPointer++];
                            byte iVelocity = data[iPlayPointer++]; // release velocity
                            cmfNoteOff(iChannel, iNote, iVelocity);
                            break;
                        }
                    case 0x90:
                        { // Note on (two data bytes)
                            byte iNote = data[iPlayPointer++];
                            byte iVelocity = data[iPlayPointer++]; // attack velocity
                            if (iVelocity != 0)
                            {
                                if (iNotePlaying[iChannel] == iNote)
                                { // Note duplicated, turn it off
                                    iVelocity = 0;
                                    // Fix this on next Note Off event
                                    bNoteFix[iChannel] = true;
                                }
                            }
                            else
                            {
                                if (bNoteFix[iChannel])
                                { // Turn on this note again
                                    iVelocity = 127;
                                    // Fix not needed anymore
                                    bNoteFix[iChannel] = false;
                                }
                            }
                            // Store last played note
                            iNotePlaying[iChannel] = (byte)(iVelocity != 0 ? iNote : 255);
                            if (iVelocity != 0)
                            {
                                cmfNoteOn(iChannel, iNote, iVelocity);
                            }
                            else
                            {
                                // This is a note-off instead (velocity == 0)
                                cmfNoteOff(iChannel, iNote, iVelocity); // 64 is the MIDI default note-off velocity
                                break;
                            }
                            break;
                        }
                    case 0xA0:
                        { // Polyphonic key pressure (two data bytes)
                            byte iNote = data[iPlayPointer++];
                            byte iPressure = data[iPlayPointer++];
                            AdPlug_LogWrite("CMF: Key pressure not yet implemented! (wanted ch%d/note %d set to %d)\n", iChannel, iNote, iPressure);
                            break;
                        }
                    case 0xB0:
                        { // Controller (two data bytes)
                            byte iController = data[iPlayPointer++];
                            byte iValue = data[iPlayPointer++];
                            MIDIcontroller(iChannel, iController, iValue);
                            break;
                        }
                    case 0xC0:
                        { // Instrument change (one data byte)
                            byte iNewInstrument = data[iPlayPointer++];
                            chMIDI[iChannel].iPatch = iNewInstrument;
                            AdPlug_LogWrite("CMF: Remembering MIDI channel %d now uses patch %d\n", iChannel, iNewInstrument);
                            break;
                        }
                    case 0xD0:
                        { // Channel pressure (one data byte)
                            byte iPressure = data[iPlayPointer++];
                            AdPlug_LogWrite("CMF: Channel pressure not yet implemented! (wanted ch%d set to %d)\n", iChannel, iPressure);
                            break;
                        }
                    case 0xE0:
                        { // Pitch bend (two data bytes)
                            byte iLSB = data[iPlayPointer++];
                            byte iMSB = data[iPlayPointer++];
                            ushort iValue = (ushort)((iMSB << 7) | iLSB);
                            // 8192 is middle/off, 0 is -2 semitones, 16384 is +2 semitones
                            chMIDI[iChannel].iPitchbend = iValue;
                            cmfNoteUpdate(iChannel);
                            AdPlug_LogWrite("CMF: Channel %d pitchbent to %d (%+.2f)\n", iChannel + 1, iValue, (float)(iValue - 8192) / 8192);
                            break;
                        }
                    case 0xF0: // System message (arbitrary data bytes)
                        switch (iCommand)
                        {
                            case 0xF0:
                                { // Sysex
                                    byte iNextByte;
                                    AdPlug_LogWrite("Sysex message: ");
                                    do
                                    {
                                        iNextByte = data[iPlayPointer++];
                                        AdPlug_LogWrite("%02X", iNextByte);
                                    } while ((iNextByte & 0x80) == 0);
                                    AdPlug_LogWrite("\n");
                                    // This will have read in the terminating EOX (0xF7) message too
                                    break;
                                }
                            case 0xF1: // MIDI Time Code Quarter Frame
                                iPlayPointer++; // message data (ignored)
                                break;
                            case 0xF2: // Song position pointer
                                iPlayPointer++; // message data (ignored)
                                iPlayPointer++;
                                break;
                            case 0xF3: // Song select
                                iPlayPointer++; // message data (ignored)
                                AdPlug_LogWrite("CMF: MIDI Song Select is not implemented.\n");
                                break;
                            case 0xF6: // Tune request
                                break;
                            case 0xF7: // End of System Exclusive (EOX) - should never be read, should be absorbed by Sysex handling code
                                break;

                            // These messages are "real time", meaning they can be sent between
                            // the bytes of other messages - but we're lazy and don't handle these
                            // here (hopefully they're not necessary in a MIDI file, and even less
                            // likely to occur in a CMF.)
                            case 0xF8: // Timing clock (sent 24 times per quarter note, only when playing)
                            case 0xFA: // Start
                            case 0xFB: // Continue
                            case 0xFE: // Active sensing (sent every 300ms or MIDI connection assumed lost)
                                break;
                            case 0xFC: // Stop
                                AdPlug_LogWrite("CMF: Received Real Time Stop message (0xFC)\n");
                                bSongEnd = true;
                                iPlayPointer = 0; // for repeat in endless-play mode
                                break;
                            case 0xFF:
                                { // System reset, used as meta-events in a MIDI file
                                    byte iEvent = data[iPlayPointer++];
                                    switch (iEvent)
                                    {
                                        case 0x2F: // end of track
                                            AdPlug_LogWrite("CMF: End-of-track, stopping playback\n");
                                            bSongEnd = true;
                                            iPlayPointer = 0; // for repeat in endless-play mode
                                            break;
                                        default:
                                            AdPlug_LogWrite("CMF: Unknown MIDI meta-event 0xFF 0x%02X\n", iEvent);
                                            break;
                                    }
                                    break;
                                }
                            default:
                                AdPlug_LogWrite("CMF: Unknown MIDI system command 0x%02X\n", iCommand);
                                break;
                        }
                        break;
                    default:
                        AdPlug_LogWrite("CMF: Unknown MIDI command 0x%02X\n", iCommand);
                        break;
                }

                if (iPlayPointer >= iSongLen)
                {
                    bSongEnd = true;
                    iPlayPointer = 0; // for repeat in endless-play mode
                }

                // Read in the number of ticks until the next event
                iDelayRemaining = readMIDINumber();
            }

            return !bSongEnd;
        }

        private void cmfNoteUpdate(byte iChannel)
        {
            byte iBlock = 0;
            ushort iOPLFNum = 0;

            // See if we're playing a rhythm mode percussive instrument
            if ((iChannel > 10) && (bPercussive))
            {
                byte iPercChannel = getPercChannel(iChannel);
                getFreq(iChannel, (byte)chOPL[iPercChannel].iMIDINote, out iBlock, out iOPLFNum);

                // Update note frequency
                writeOPL((byte)(BASE_FNUM_L + iPercChannel), (byte)(iOPLFNum & 0xFF));
                writeOPL((byte)(BASE_KEYON_FREQ + iPercChannel), (byte)((iBlock << 2) | ((iOPLFNum >> 8) & 0x03)));

            }
            else
            { // Non rhythm-mode or a normal instrument channel

                // Figure out which OPL channels should be updated
                int iNumChannels = bPercussive ? 6 : 9;
                for (int i = 0; i < iNumChannels; i++)
                {
                    // Needed channel and note is playing
                    if (chOPL[i].iMIDIChannel == iChannel && chOPL[i].iNoteStart > 0)
                    {
                        // Update note frequency
                        getFreq(iChannel, (byte)chOPL[i].iMIDINote, out iBlock, out iOPLFNum);
                        writeOPL((byte)(BASE_FNUM_L + i), (byte)(iOPLFNum & 0xFF));
                        writeOPL((byte)(BASE_KEYON_FREQ + i), (byte)(OPLBIT_KEYON | (iBlock << 2) | ((iOPLFNum & 0x300) >> 8)));
                    }
                }
            }
        }

        private void getFreq(byte iChannel, byte iNote, out byte iBlock, out ushort iOPLFNum)
        {
            iBlock = (byte)(iNote / 12);
            if (iBlock > 1) (iBlock)--; // keep in the same range as the Creative player
                                        //if (*iBlock > 7) *iBlock = 7; // don't want to go out of range

            double d = Math.Pow(2, (
              (double)iNote + (
                (chMIDI[iChannel].iPitchbend - 8192) / 8192.0
              ) + (
                chMIDI[iChannel].iTranspose / 256.0
              ) - 9) / 12.0 - (iBlock - 20))
              * 440.0 / 32.0 / 50000.0;
            iOPLFNum = (ushort)(d + 0.5);
        }

        private byte getPercChannel(byte iChannel)
        {
            switch (iChannel)
            {
                case 11: return 7 - 1; // Bass drum
                case 12: return 8 - 1; // Snare drum
                case 13: return 9 - 1; // Tom tom
                case 14: return 9 - 1; // Top cymbal
                case 15: return 8 - 1; // Hihat
            }
            AdPlug_LogWrite("CMF ERR: Tried to get the percussion channel from MIDI channel %d - this shouldn't happen!\n", iChannel);
            return 0;
        }

        private void MIDIcontroller(byte iChannel, byte iController, byte iValue)
        {
            switch (iController)
            {
                case 0x63:
                    // Custom extension to allow CMF files to switch the AM+VIB depth on and
                    // off (officially both are on, and there's no way to switch them off.)
                    // Controller values:
                    //   0 == AM+VIB off
                    //   1 == VIB on
                    //   2 == AM on
                    //   3 == AM+VIB on
                    if (iValue != 0)
                    {
                        writeOPL(BASE_RHYTHM, (byte)((iCurrentRegs[BASE_RHYTHM] & ~0xC0) | (iValue << 6))); // switch AM+VIB extension on
                    }
                    else
                    {
                        writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] & ~0xC0)); // switch AM+VIB extension off
                    }
                    AdPlug_LogWrite("CMF: AM+VIB depth change - AM %s, VIB %s\n",
                      ((iCurrentRegs[BASE_RHYTHM] & 0x80) != 0) ? "on" : "off",
                      ((iCurrentRegs[BASE_RHYTHM] & 0x40) != 0) ? "on" : "off");
                    break;
                case 0x66:
                    AdPlug_LogWrite("CMF: Song set marker to 0x%02X\n", iValue);
                    break;
                case 0x67:
                    bPercussive = (iValue != 0);
                    if (bPercussive)
                    {
                        writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] | 0x20)); // switch rhythm-mode on
                    }
                    else
                    {
                        writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] & ~0x20)); // switch rhythm-mode off
                    }
                    AdPlug_LogWrite("CMF: Percussive/rhythm mode %s\n", bPercussive ? "enabled" : "disabled");
                    break;
                case 0x68:
                    chMIDI[iChannel].iTranspose = iValue;
                    cmfNoteUpdate(iChannel);
                    AdPlug_LogWrite("CMF: Transposing all notes up by %d * 1/128ths of a semitone on channel %d.\n", iValue, iChannel + 1);
                    break;
                case 0x69:
                    chMIDI[iChannel].iTranspose = -iValue;
                    cmfNoteUpdate(iChannel);
                    AdPlug_LogWrite("CMF: Transposing all notes down by %d * 1/128ths of a semitone on channel %d.\n", iValue, iChannel + 1);
                    break;
                default:
                    AdPlug_LogWrite("CMF: Unsupported MIDI controller 0x%02X, ignoring.\n", iController);
                    break;
            }
        }

        private void AdPlug_LogWrite(string fmt, params object[] parameters)
        {
        }

        private void cmfNoteOff(byte iChannel, byte iNote, byte iVelocity)
        {
            if ((iChannel > 10) && (bPercussive))
            {
                int iOPLChannel = getPercChannel(iChannel);
                if (chOPL[iOPLChannel].iMIDINote != iNote) return; // there's a different note playing now
                writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] & ~(1 << (15 - iChannel))));
                chOPL[iOPLChannel].iNoteStart = 0; // channel free
            }
            else
            { // Non rhythm-mode or a normal instrument channel
                int iOPLChannel = -1;
                int iNumChannels = bPercussive ? 6 : 9;
                for (int i = 0; i < iNumChannels; i++)
                {
                    if (
                      (chOPL[i].iMIDIChannel == iChannel) &&
                      (chOPL[i].iMIDINote == iNote) &&
                      (chOPL[i].iNoteStart != 0)
                    )
                    {
                        // Found the note, switch it off
                        chOPL[i].iNoteStart = 0;
                        iOPLChannel = i;
                        break;
                    }
                }
                if (iOPLChannel == -1) return;

                writeOPL((byte)(BASE_KEYON_FREQ + iOPLChannel), (byte)(iCurrentRegs[BASE_KEYON_FREQ + iOPLChannel] & ~OPLBIT_KEYON));
            }
        }

        private void cmfNoteOn(byte iChannel, byte iNote, byte iVelocity)
        {
            byte iBlock = 0;
            ushort iOPLFNum = 0;
            getFreq(iChannel, iNote, out iBlock, out iOPLFNum);
            if (iOPLFNum > 1023) AdPlug_LogWrite("CMF: This note is out of range! (send this song to malvineous@shikadi.net!)\n");

            // See if we're playing a rhythm mode percussive instrument
            if ((iChannel > 10) && (bPercussive))
            {
                byte iPercChannel = getPercChannel(iChannel);

                // Will have to set every time (easier) than figuring out whether the mod
                // or car needs to be changed.
                //if (chOPL[iPercChannel].iMIDIPatch != chMIDI[iChannel].iPatch) {
                MIDIchangeInstrument(iPercChannel, iChannel, (byte)(chMIDI[iChannel].iPatch));
                //}

                /*  Velocity calculations - TODO: Work out the proper formula

                iVelocity -> iLevel  (values generated by Creative's player)
                7f -> 00
                7c -> 00

                7b -> 09
                73 -> 0a
                6b -> 0b
                63 -> 0c
                5b -> 0d
                53 -> 0e
                4b -> 0f
                43 -> 10
                3b -> 11
                33 -> 13
                2b -> 15
                23 -> 19
                1b -> 1b
                13 -> 1d
                0b -> 1f
                03 -> 21

                02 -> 21
                00 -> N/A (note off)
                */
                // Approximate formula, need to figure out more accurate one (my maths isn't so good...)
                int iLevel = (int)(0x25 - Math.Sqrt(iVelocity * 16/*6*/));//(127 - iVelocity) * 0x20 / 127;
                if (iVelocity > 0x7b) iLevel = 0; // full volume
                if (iLevel < 0) iLevel = 0;
                if (iLevel > 0x3F) iLevel = 0x3F;
                //if (iVelocity < 0x40) iLevel = 0x10;

                int iOPLOffset = BASE_SCAL_LEVL + OPLOFFSET(iPercChannel);
                //if ((iChannel == 11) || (iChannel == 12) || (iChannel == 14)) {
                if (iChannel == 11) iOPLOffset += 3; // only do bassdrum carrier for volume control
                                                     //iOPLOffset += 3; // carrier
                writeOPL((byte)iOPLOffset, (byte)((iCurrentRegs[iOPLOffset] & ~0x3F) | iLevel));//(iVelocity * 0x3F / 127));
                                                                                                //}
                                                                                                // Bass drum (ch11) uses both operators
                                                                                                //if (iChannel == 11) writeOPL(iOPLOffset + 3, (iCurrentRegs[iOPLOffset + 3] & ~0x3F) | iLevel);

                /*		#ifdef USE_VELOCITY  // Official CMF player seems to ignore velocity levels
                      ushort iLevel = 0x2F - (iVelocity * 0x2F / 127); // 0x2F should be 0x3F but it's too quiet then
                      AdPlug_LogWrite("%02X + vel %d (lev %02X) == %02X\n", iCurrentRegs[iOPLOffset], iVelocity, iLevel, (iCurrentRegs[iOPLOffset] & ~0x3F) | iLevel);
                      //writeOPL(iOPLOffset, (iCurrentRegs[iOPLOffset] & ~0x3F) | (0x3F - (iVelocity >> 1)));//(iVelocity * 0x3F / 127));
                      writeOPL(iOPLOffset, (iCurrentRegs[iOPLOffset] & ~0x3F) | iLevel);//(iVelocity * 0x3F / 127));
                    #endif*/

                // Apparently you can't set the frequency for the cymbal or hihat?
                // Vinyl requires you don't set it, Kiloblaster requires you do!
                writeOPL((byte)(BASE_FNUM_L + iPercChannel), (byte)(iOPLFNum & 0xFF));
                writeOPL((byte)(BASE_KEYON_FREQ + iPercChannel), (byte)((iBlock << 2) | ((iOPLFNum >> 8) & 0x03)));

                byte iBit = (byte)(1 << (15 - iChannel));

                // Turn the perc instrument off if it's already playing (OPL can't do
                // polyphonic notes w/ percussion)
                if ((iCurrentRegs[BASE_RHYTHM] & iBit) != 0) writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] & ~iBit));

                // I wonder whether we need to delay or anything here?

                // Turn the note on
                //if (iChannel == 15) {
                writeOPL(BASE_RHYTHM, (byte)(iCurrentRegs[BASE_RHYTHM] | iBit));
                //AdPlug_LogWrite("CMF: Note %d on MIDI channel %d (mapped to OPL channel %d-1) - vel %02X, fnum %d/%d\n", iNote, iChannel, iPercChannel+1, iVelocity, iOPLFNum, iBlock);
                //}

                chOPL[iPercChannel].iNoteStart = ++iNoteCount;
                chOPL[iPercChannel].iMIDIChannel = iChannel;
                chOPL[iPercChannel].iMIDINote = iNote;

            }
            else
            { // Non rhythm-mode or a normal instrument channel

                // Figure out which OPL channel to play this note on
                int iOPLChannel = -1;
                int iNumChannels = bPercussive ? 6 : 9;
                for (int i = iNumChannels - 1; i >= 0; i--)
                {
                    // If there's no note playing on this OPL channel, use that
                    if (chOPL[i].iNoteStart == 0)
                    {
                        iOPLChannel = i;
                        // See if this channel is already set to the instrument we want.
                        if (chOPL[i].iMIDIPatch == chMIDI[iChannel].iPatch)
                        {
                            // It is, so stop searching
                            break;
                        } // else keep searching just in case there's a better match
                    }
                }
                if (iOPLChannel == -1)
                {
                    // All channels were in use, find the one with the longest note
                    iOPLChannel = 0;
                    int iEarliest = chOPL[0].iNoteStart;
                    for (int i = 1; i < iNumChannels; i++)
                    {
                        if (chOPL[i].iNoteStart < iEarliest)
                        {
                            // Found a channel with a note being played for longer
                            iOPLChannel = i;
                            iEarliest = chOPL[i].iNoteStart;
                        }
                    }
                    AdPlug_LogWrite("CMF: Too many polyphonic notes, cutting note on channel %d\n", iOPLChannel);
                }

                // Run through all the channels with negative notestart values - these
                // channels have had notes recently stop - and increment the counter
                // to slowly move the channel closer to being reused for a future note.
                //for (int i = 0; i < iNumChannels; i++) {
                //	if (chOPL[i].iNoteStart < 0) chOPL[i].iNoteStart++;
                //}

                // Now the new note should be played on iOPLChannel, but see if the instrument
                // is right first.
                if (chOPL[iOPLChannel].iMIDIPatch != chMIDI[iChannel].iPatch)
                {
                    MIDIchangeInstrument((byte)iOPLChannel, iChannel, (byte)chMIDI[iChannel].iPatch);
                }

                chOPL[iOPLChannel].iNoteStart = ++iNoteCount;
                chOPL[iOPLChannel].iMIDIChannel = iChannel;
                chOPL[iOPLChannel].iMIDINote = iNote;

                // #ifdef USE_VELOCITY  // Official CMF player seems to ignore velocity levels
                // 	// Adjust the channel volume to match the note velocity
                // 	byte iOPLOffset = BASE_SCAL_LEVL + OPLOFFSET(iChannel) + 3; // +3 == Carrier
                // 	ushort iLevel = 0x2F - (iVelocity * 0x2F / 127); // 0x2F should be 0x3F but it's too quiet then
                // 	writeOPL(iOPLOffset, (iCurrentRegs[iOPLOffset] & ~0x3F) | iLevel);
                // #endif

                // Set the frequency and play the note
                writeOPL((byte)(BASE_FNUM_L + iOPLChannel), (byte)(iOPLFNum & 0xFF));
                writeOPL((byte)(BASE_KEYON_FREQ + iOPLChannel), (byte)(OPLBIT_KEYON | (iBlock << 2) | ((iOPLFNum & 0x300) >> 8)));
            }
        }

        private void MIDIchangeInstrument(byte iOPLChannel, byte iMIDIChannel, byte iNewInstrument)
        {
            if ((iMIDIChannel > 10) && (bPercussive))
            {
                switch (iMIDIChannel)
                {
                    case 11: // Bass drum (operator 13+16 == channel 7 modulator+carrier)
                        writeInstrumentSettings(7 - 1, 0, 0, iNewInstrument);
                        writeInstrumentSettings(7 - 1, 1, 1, iNewInstrument);
                        break;
                    case 12: // Snare drum (operator 17 == channel 8 carrier)
                             //case 15:
                        writeInstrumentSettings(8 - 1, 0, 1, iNewInstrument);

                        //
                        //writeInstrumentSettings(8-1, 0, 0, iNewInstrument);
                        break;
                    case 13: // Tom tom (operator 15 == channel 9 modulator)
                             //case 14:
                        writeInstrumentSettings(9 - 1, 0, 0, iNewInstrument);

                        //
                        //writeInstrumentSettings(9-1, 0, 1, iNewInstrument);
                        break;
                    case 14: // Top cymbal (operator 18 == channel 9 carrier)
                        writeInstrumentSettings(9 - 1, 0, 1, iNewInstrument);
                        break;
                    case 15: // Hi-hat (operator 14 == channel 8 modulator)
                        writeInstrumentSettings(8 - 1, 0, 0, iNewInstrument);
                        break;
                    default:
                        AdPlug_LogWrite("CMF: Invalid MIDI channel %d (not melodic and not percussive!)\n", iMIDIChannel + 1);
                        break;
                }
                chOPL[iOPLChannel].iMIDIPatch = iNewInstrument;
            }
            else
            {
                // Standard nine OPL channels
                writeInstrumentSettings(iOPLChannel, 0, 0, iNewInstrument);
                writeInstrumentSettings(iOPLChannel, 1, 1, iNewInstrument);
                chOPL[iOPLChannel].iMIDIPatch = iNewInstrument;
            }
        }

        // iChannel: OPL channel (0-8)
        // iOperator: 0 == Modulator, 1 == Carrier
        //   Source - source operator to read from instrument definition
        //   Dest - destination operator on OPL chip
        // iInstrument: Index into pInstruments array of CMF instruments
        private void writeInstrumentSettings(byte iChannel, byte iOperatorSource, byte iOperatorDest, byte iInstrument)
        {
            // assert(iChannel <= 8);

            byte iOPLOffset = (byte)OPLOFFSET(iChannel);
            if (iOperatorDest != 0) iOPLOffset += 3; // Carrier if iOperator == 1 (else Modulator)

            writeOPL((byte)(BASE_CHAR_MULT + iOPLOffset), pInstruments[iInstrument].op[iOperatorSource].iCharMult);
            writeOPL((byte)(BASE_SCAL_LEVL + iOPLOffset), pInstruments[iInstrument].op[iOperatorSource].iScalingOutput);
            writeOPL((byte)(BASE_ATCK_DCAY + iOPLOffset), pInstruments[iInstrument].op[iOperatorSource].iAttackDecay);
            writeOPL((byte)(BASE_SUST_RLSE + iOPLOffset), pInstruments[iInstrument].op[iOperatorSource].iSustainRelease);
            writeOPL((byte)(BASE_WAVE + iOPLOffset), pInstruments[iInstrument].op[iOperatorSource].iWaveSel);

            // TODO: Check to see whether we should only be loading this for one or both operators
            writeOPL((byte)(BASE_FEED_CONN + iChannel), pInstruments[iInstrument].iConnection);
        }

        private void Rewind(int subsong)
        {
            // Initialise

            // Enable use of WaveSel register on OPL3 (even though we're only an OPL2!)
            // Apparently this enables nine-channel mode?
            writeOPL(0x01, 0x20);

            // Disable OPL3 mode (can be left enabled by a previous non-CMF song)
            writeOPL(0x05, 0x00);

            // Really make sure CSM+SEL are off (again, Creative's player...)
            writeOPL(0x08, 0x00);

            // This freq setting is required for the hihat to sound correct at the start
            // of funky.cmf, even though it's for an unrelated channel.
            // If it's here however, it makes the hihat in Word Rescue's theme.cmf
            // sound really bad.
            // TODO: How do we figure out whether we need it or not???
            writeOPL(BASE_FNUM_L + 8, 514 & 0xFF);
            writeOPL(BASE_KEYON_FREQ + 8, (1 << 2) | (514 >> 8));

            // default freqs?
            writeOPL(BASE_FNUM_L + 7, 509 & 0xFF);
            writeOPL(BASE_KEYON_FREQ + 7, (2 << 2) | (509 >> 8));
            writeOPL(BASE_FNUM_L + 6, 432 & 0xFF);
            writeOPL(BASE_KEYON_FREQ + 6, (2 << 2) | (432 >> 8));

            // Amplify AM + VIB depth.  Creative's CMF player does this, and there
            // doesn't seem to be any way to stop it from doing so - except for the
            // non-standard controller 0x63 I added :-)
            writeOPL(0xBD, 0xC0);

            bSongEnd = false;
            iPlayPointer = 0;
            iPrevCommand = 0; // just in case
            iNoteCount = 0;

            // Read in the number of ticks until the first event
            iDelayRemaining = readMIDINumber();

            // Reset song state.  This used to be in the constructor, but the XMMS2
            // plugin sets the song length before starting playback.  AdPlug plays the
            // song in its entirety (with no synth) to determine the song length, which
            // results in the state variables below matching the end of the song.  When
            // the real OPL synth is activated for playback, it no longer matches the
            // state variables and the instruments are not set correctly!
            for (int i = 0; i < 9; i++)
            {
                chOPL[i].iNoteStart = 0; // no note playing atm
                chOPL[i].iMIDINote = -1;
                chOPL[i].iMIDIChannel = -1;
                chOPL[i].iMIDIPatch = -1;

                chMIDI[i].iPatch = -2;
                chMIDI[i].iPitchbend = 8192;
                chMIDI[i].iTranspose = 0;
            }
            for (int i = 9; i < 16; i++)
            {
                chMIDI[i].iPatch = -2;
                chMIDI[i].iPitchbend = 8192;
                chMIDI[i].iTranspose = 0;
            }

            Array.Clear(iCurrentRegs, 0, 256);
            for (int i = 0; i < iNotePlaying.Length; i++)
            {
                iNotePlaying[i] = 255;
            }
            Array.Clear(bNoteFix, 0, 16);
        }

        // Write a byte to the OPL "chip" and update the current record of register states
        private void writeOPL(byte iRegister, byte iValue)
        {
            Opl.WriteReg(iRegister, iValue);
            iCurrentRegs[iRegister] = iValue;
        }

        // Read a variable-length integer from MIDI data
        private int readMIDINumber()
        {
            int iValue = 0;
            for (int i = 0; i < 4; i++)
            {
                byte iNext = data[iPlayPointer++];
                iValue <<= 7;
                iValue |= (iNext & 0x7F); // ignore the MSB
                if ((iNext & 0x80) == 0) break; // last byte has the MSB unset
            }
            return iValue;
        }

        private static string ReadString(BinaryReader br, long length = long.MaxValue)
        {
            var text = new StringBuilder();
            char c;
            long i = 0;
            while ((c = br.ReadChar()) != 0 && i++ < length)
            {
                text.Append(c);
            }
            return text.ToString();
        }
    }
}
