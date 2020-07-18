//
//  Dro2Player.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2020
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
    public class Dro2Player: IMusicPlayer
    {
		private byte iCmdDelayS, iCmdDelayL;
		private int iConvTableLen;
		private byte[] piConvTable;

		private byte[] data;
		private int iLength;
		private int iPos;
		private int iDelay;
		private string author;
		private string desc;
		private string title;

		public IOpl Opl { get; }

		public float RefreshRate
		{
			get
			{
				if (iDelay > 0) return 1000.0f / iDelay;
				else return 1000.0f;
			}
		}

		public Dro2Player(IOpl opl)
		{
			if (opl == null) throw new ArgumentNullException(nameof(opl));
			Opl = opl;
		}

		public bool Load(string path)
		{
			using (var fs = File.OpenRead(path))
			{
				var br = new BinaryReader(fs);
				var id = new string(br.ReadChars(8));
				if (id != "DBRAWOPL") return false;

				var version = br.ReadInt32();
				if (version != 0x2) return false;

				iLength = br.ReadInt32(); // should better use an unsigned type
				if (iLength <= 0 || iLength >= 1 << 30 ||
					iLength > br.BaseStream.Length - br.BaseStream.Position)
				{
					return false;
				}
				iLength *= 2; // stored in file as number of byte p
				br.BaseStream.Seek(4, SeekOrigin.Current);   // Length in milliseconds
				br.BaseStream.Seek(1, SeekOrigin.Current);   /// OPL type (0 == OPL2, 1 == Dual OPL2, 2 == OPL3)
				int iFormat = br.ReadByte();
				if (iFormat != 0)
				{
					return false;
				}
				int iCompression = br.ReadByte();
				if (iCompression != 0)
				{
					return false;
				}
				iCmdDelayS = br.ReadByte();
				iCmdDelayL = br.ReadByte();
				iConvTableLen = br.ReadByte();

				piConvTable = br.ReadBytes(iConvTableLen);

				// Read the OPL data.
				data = br.ReadBytes(iLength);

				int tagsize = (int)(br.BaseStream.Length - br.BaseStream.Position);
				if (tagsize >= 3)
				{
					// The arbitrary Tag Data section begins here.
					if (br.ReadByte() != 0xFF ||
						br.ReadByte() != 0xFF ||
						br.ReadByte() != 0x1A)
					{
						// Tag data does not present or truncated.
						goto end_section;
					}

					// "title" is maximum 40 characters long.
					title = ReadString(br, 40);

					// Skip "author" if Tag marker byte is missing.
					if (br.ReadByte() != 0x1B)
					{
						br.BaseStream.Seek(-1, SeekOrigin.Current);
						goto desc_section;
					}

					// "author" is maximum 40 characters long.
					author = ReadString(br, 40);

				desc_section:
					// Skip "desc" if Tag marker byte is missing.
					if (br.ReadByte() != 0x1C)
					{
						goto end_section;
					}

					// "desc" is now maximum 1023 characters long (it was 140).
					desc = ReadString(br, 1023);
				}

			end_section:
				Rewind(0);

				return true;
			}
		}

		public bool Update()
        {
			while (iPos < iLength)
			{
				int iIndex = data[iPos++];
				int iValue = data[iPos++];

				// Short delay
				if (iIndex == iCmdDelayS)
				{
					iDelay = iValue + 1;
					return true;

					// Long delay
				}
				else if (iIndex == iCmdDelayL)
				{
					iDelay = (iValue + 1) << 8;
					return true;

					// Normal write
				}
				else
				{
					if ((iIndex & 0x80)!=0)
					{
						// High bit means use second chip in dual-OPL2 config
						// TODO:?
						//Opl.setchip(1);
						iIndex &= 0x7F;
					}
					else
					{
						// TODO:?
						//Opl.Setchip(0);
					}
					if (iIndex >= iConvTableLen)
					{
						Console.WriteLine("DRO2: Error - index beyond end of codemap table!  Corrupted .dro?\n");
						return false; // EOF
					}
					int iReg = piConvTable[iIndex];
					Opl.WriteReg(iReg, iValue);
				}

			}

			// This won't result in endless-play using Adplay, but IMHO that code belongs
			// in Adplay itself, not here.
			return iPos < iLength;
		}

		public void Rewind(int subsong)
        {
			iDelay = 0;
			iPos = 0;
		}

		private static string ReadString(BinaryReader br, int maxLength)
        {
			char c;
			int i = 0;
			var text = new StringBuilder();
			while ((c = br.ReadChar()) != 0 && i<maxLength)
			{
				text.Append(c);
				i++;
			}
			return text.ToString();
		}
		
	}
}
