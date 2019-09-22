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
    /// ADL player adaption by Simon Peter dn.tlp@gmx.net
    /// This code has been adapted from adplug https://github.com/adplug/adplug
    /// Original ADL player by Torbjorn Andersson and Johannes Schickel
    /// 'lordhoto' lordhoto at scummvm dot org of the ScummVM project.
    /// </summary>
    internal sealed class AdlPlayer : IMusicPlayer
    {
        int numsubsongs, cursubsong;
        AdlibDriver _driver;
        byte _version;
        byte[] _trackEntries = new byte[120];
        ushort[] _trackEntries16 = new ushort[250];
        byte[] _soundDataPtr;
        int _sfxPlayingSound;
        byte _sfxPriority;
        byte _sfxFourthByteOfSong;
        public IOpl Opl { get; }

        // refresh rate is fixed at 72Hz
        public float RefreshRate => 72.0f;

        public AdlPlayer(IOpl opl)
        {
            if (opl == null) throw new ArgumentNullException(nameof(opl));
            Opl = opl;
            _driver = new AdlibDriver(opl);
        }

        public bool Load(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".adl", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Init();
            using (var fs = File.OpenRead(path))
            {
                if (fs.Length < 720) return false;

                unk2();
                unk1();

                // detect format version
                var br = new BinaryReader(fs);
                _version = 3; // assuming we have v3
                for (int i = 0; i < 120; i += 2)
                {
                    ushort w = br.ReadUInt16();
                    // all entries should be in range 0..500-1 or 0xFFFF
                    if (w >= 500 && w < 0xffff)
                    {
                        _version = 1; // actually 1 or 2
                        break;
                    }
                }
                if (_version == 1)
                { // detect whether v1 or v2
                    fs.Seek(120, SeekOrigin.Begin);
                    _version = 2; // assuming we have v2
                    for (int i = 0; i < 150; i += 2)
                    {
                        ushort w = br.ReadUInt16();
                        if (w > 0 && w < 600)
                        { // minimum track offset for v1 is 600
                            return false;
                        }
                        // minimum track offset for v2 is 1000
                        if (w > 0 && w < 1000)
                            _version = 1;
                    }
                }
                if (_version == 2 && fs.Length < 1120)
                { // minimum file size of v2
                    return false;
                }
                if (_version == 3 && fs.Length < 2500)
                { // minimum file size of v3
                    return false;
                }

                fs.Seek(0, SeekOrigin.Begin);
                var file_size = (int)fs.Length;

                _driver.snd_unkOpcode3(-1);
                _soundDataPtr = null;

                ushort _EntriesSize;
                if (_version < 3)
                {
                    _EntriesSize = 120;
                    _trackEntries = br.ReadBytes(_EntriesSize);
                }
                else
                {
                    _EntriesSize = 250 * 2;
                    for (int i = 0; i < 250; i++)
                    {
                        _trackEntries16[i] = br.ReadUInt16();
                    }
                }

                int soundDataSize = file_size - _EntriesSize;

                _soundDataPtr = br.ReadBytes(soundDataSize);

                file_size = 0;

                _driver.snd_setSoundData(_soundDataPtr);

                // 	_soundFileLoaded = file;

                // find last subsong
                ushort maxEntry = 0xffff;
                switch (_version)
                {
                    case 1:
                        maxEntry = 150 - 1;
                        break;
                    case 2:
                        maxEntry = 250 - 1;
                        break;
                    case 3:
                        maxEntry = 500 - 1;
                        break;
                }
                if (_version < 3)
                {
                    for (int i = 120 - 1; i >= 0; i--)
                        if (_trackEntries[i] <= maxEntry)
                        {
                            numsubsongs = i + 1;
                            break;
                        }
                }
                else
                {
                    for (int i = 250 - 1; i >= 0; i--)
                        if (_trackEntries16[i] <= maxEntry)
                        {
                            numsubsongs = i + 1;
                            break;
                        }
                }

                cursubsong = -1;
                return true;
            }
        }

        public bool Update()
        {
            if (cursubsong == -1)
                Rewind(2);

            _driver.Callback();

            bool songend = true;
            for (int i = 0; i < 10; i++)
                if (_driver._channels[i].dataptr != -1)
                    songend = false;

            return !songend;
        }

        private bool Init()
        {
            _driver.snd_initDriver();
            _driver.snd_setFlag(4);
            return true;
        }

        private void Rewind(int subsong)
        {
            if (subsong == -1) subsong = cursubsong;
            Opl.WriteReg(1, 32);
            playSoundEffect((ushort)subsong);
            cursubsong = subsong;
            Update();
        }

        private void unk1()
        {
            playSoundEffect(0);
        }

        private void unk2()
        {
            playSoundEffect(0);
        }

        private void playSoundEffect(ushort track)
        {
            play(track);
        }

        private void play(ushort track)
        {
            ushort soundId = 0;
            if (_version < 3)
            {
                soundId = _trackEntries[track];
                if ((sbyte)soundId == -1 || _soundDataPtr == null)
                    return;
                soundId &= 0xFF;
            }
            else
            {
                soundId = _trackEntries16[track];
                if ((short)soundId == -1 || _soundDataPtr == null)
                    return;
                soundId &= 0xFFFF;
            }
            _driver.ADLVersion = _version;
            _driver.snd_setFlag(0);
            // 	while ((_driver.callback(16, 0) & 8)) {
            // We call the system delay and not the game delay to avoid concurrency issues.
            // 		_engine._system.delayMillis(10);
            // 	}
            if (_sfxPlayingSound != -1)
            {
                // Restore the sounds's normal values.
                _driver.snd_writeByte(_sfxPlayingSound, 1, _sfxPriority);
                _driver.snd_writeByte(_sfxPlayingSound, 3, _sfxFourthByteOfSong);
                _sfxPlayingSound = -1;
            }

            int chan = _driver.snd_readByte(soundId, 0);

            if (chan != 9)
            {
                _sfxPlayingSound = soundId;
                _sfxPriority = (byte)_driver.snd_readByte(soundId, 1);
                _sfxFourthByteOfSong = (byte)_driver.snd_readByte(soundId, 3);

                // In the cases I've seen, the mysterious fourth byte has been
                // the parameter for the update_setExtraLevel3() callback.
                //
                // The extra level is part of the channels "total level", which
                // is a six-bit value where larger values means softer volume.
                //
                // So what seems to be happening here is that sounds which are
                // started by this function are given a slightly lower priority
                // and a slightly higher (i.e. softer) extra level 3 than they
                // would have if they were started from anywhere else. Strange.

                int newVal = ((((-_sfxFourthByteOfSong) + 63) * 0xFF) >> 8) & 0xFF;
                newVal = -newVal + 63;
                _driver.snd_writeByte(soundId, 3, (byte)newVal);
                newVal = ((_sfxPriority * 0xFF) >> 8) & 0xFF;
                _driver.snd_writeByte(soundId, 1, (byte)newVal);
            }

            _driver.snd_startSong(soundId);
        }
    }
}
