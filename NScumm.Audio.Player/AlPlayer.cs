﻿//
//  AlPlayer.cs
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
using System.Threading;
using NScumm.Audio.Players;
using OpenAL;

namespace NScumm.Audio.AlPlayer
{
    internal sealed class AlPlayer
    {
        private const int DataChunckSize = 1024 * 4;
        private const int NumBuffers = 4;

        private readonly IMusicPlayer _player;
        private readonly int _rate;
        private readonly int _channels;
        private readonly IntPtr _device;
        private readonly IntPtr _context;
        private int minicnt;

        public AlPlayer(IMusicPlayer player, int rate, int channels)
        {
            _player = player;
            _rate = rate;
            _channels = channels;

            _device = Alc.OpenDevice(null);
            _context = Alc.CreateContext(_device, null);

            Console.WriteLine(Alc.GetString(IntPtr.Zero, Alc.AllDevicesSpecifier));

            Alc.MakeContextCurrent(_context);
        }

        public void Play()
        {
            var thread = new Thread(new ThreadStart(AudioPlayerThread))
            {
                IsBackground = true
            };
            thread.Start();
        }

        private int GetDataChunk(short[] data)
        {
            int freq = _rate;

            int i, towrite = DataChunckSize / _channels;
            var pos = 0;

            while (towrite > 0)
            {
                while (minicnt < 0)
                {
                    minicnt += freq;
                    var playing = _player.Update();
                    if (!playing)
                        return -1;
                }
                i = Math.Min(towrite, (int)(minicnt / _player.RefreshRate + 4) & ~3);
                _player.Opl.ReadBuffer(data, pos, i);
                pos += i;
                towrite -= i;
                minicnt -= (int)(_player.RefreshRate * i);
            }

            return DataChunckSize;
        }

        private int Buffer(short[] data, uint id)
        {
            var numSamples = GetDataChunk(data);
            if (numSamples < 0) return numSamples;
            unsafe
            {
                fixed (short* pData = data)
                {
                    var format = _channels == 2 ? Al.FormatStereo16 : Al.FormatMono16; 
                    Al.BufferData(id, format, pData, numSamples * 2, _rate);
                }
            }
            return numSamples;
        }

        private void AudioPlayerThread()
        {
            var iTotalBuffersProcessed = 0;
            var uiBuffer = new uint[1];

            Al.GenSources(1, out uint[] source);
            Al.GenBuffers(NumBuffers, out uint[] buffer);

            var data = new short[DataChunckSize];

            // Fill all the buffers with audio data from the wave file
            foreach (var id in buffer)
            {
                Buffer(data, id);
            }
            Al.SourceQueueBuffers(source[0], buffer.Length, buffer);
            Al.SourcePlay(source[0]);

            bool playing = true;
            while (playing)
            {
                Thread.Sleep(10); // Sleep 10 msec periodically

                Al.GetSourcei(source[0], Al.BuffersProcessed, out int iBuffersProcessed);

                iTotalBuffersProcessed += iBuffersProcessed;
                Console.Write("\rBuffers Processed {0}", iTotalBuffersProcessed);

                // For each processed buffer, remove it from the source queue, read the next chunk of
                // audio data from the file, fill the buffer with new data, and add it to the source queue
                while (iBuffersProcessed > 0)
                {
                    // Remove the buffer from the queue (uiBuffer contains the buffer ID for the dequeued buffer)
                    Al.SourceUnqueueBuffers(source[0], 1, uiBuffer);

                    // Read more pData audio data (if there is any)
                    if (Buffer(data, uiBuffer[0]) > 0)
                    {
                        // Insert the audio buffer to the source queue
                        Al.SourceQueueBuffers(source[0], 1, uiBuffer);
                    }
                    Al.GetSourcei(source[0], Al.SourceState, out var state);
                    if (state != Al.Playing)
                    {
                        playing = false;
                    }

                    iBuffersProcessed--;
                }
            }

            Console.WriteLine();
            Console.WriteLine("End!");

            Al.SourceStop(source[0]);
            Al.DeleteSources(source.Length, source);
            Al.DeleteBuffers(buffer.Length, buffer);

            Alc.MakeContextCurrent(IntPtr.Zero);
            Alc.DestroyContext(_context);
            Alc.CloseDevice(_device);
        }
    }
}
