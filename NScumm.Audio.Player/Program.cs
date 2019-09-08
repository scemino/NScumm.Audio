//
//  Program.cs
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
using NScumm.Core.Audio.OPL.DosBox;

namespace NScumm.Audio.Player
{
    class Program
    {
        private const int Rate = 44100;

        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("No input music file specified");
                return -1;
            }

            var opl = new DosBoxOPL(OplType.Opl2);
            opl.Init(Rate);

            var players = new IMusicPlayer[]
            {
                new DroPlayer(opl),
                new ImfPlayer(opl)
            };
            foreach (var player in players)
            {
                if (!player.Load(args[0]))
                    continue;

                var alPlayer = new AlPlayer(player, Rate);
                alPlayer.Play();

                Console.WriteLine("Hit a key to stop!");
                Console.ReadKey();
                return 0;
            }

            Console.Error.WriteLine("This music file is not supported");
            return -1;
        }
    }
}
