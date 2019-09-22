//
//  Factory.cs
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

using NScumm.Core.Audio.OPL;

namespace NScumm.Audio.Players
{
    public static class Factory
    {
        public static IMusicPlayer[] GetPlayers(IOpl opl)
        {
            return new IMusicPlayer[]
            {
                new KsmPlayer(opl),
                new CmfPlayer(opl),
                new MidPlayer(opl),
                new DroPlayer(opl),
                new ImfPlayer(opl),
                new SngPlayer(opl),
                new XsmPlayer(opl),
                new S3mPlayer(opl),
                new AdlPlayer(opl),
                new MkjPlayer(opl),
            };
        }
    }
}
