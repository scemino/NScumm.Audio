using System;

namespace NScumm.Core.Audio.OPL
{
    public sealed class SurroundOpl : IOpl
    {
        // The right-channel is increased in frequency by itself divided by this amount.
        // The right value should not noticeably change the pitch, but it should provide
        // a nice stereo harmonic effect.
        private const double FreqOffset = 128.0;//96.0
        
        // Number of FNums away from the upper/lower limit before switching to the next
        // block (octave.)  By rights it should be zero, but for some reason this seems
        // to cut it too close and the transposed OPL doesn't hit the right note all the
        // time.  Setting it higher means it will switch blocks sooner and that seems
        // to help.  Don't set it too high or it'll get stuck in an infinite loop if
        // one block is too high and the adjacent block is too low ;-)
        private const int NewBlockLimit = 32;

        private const int BuffSize = 4096;
        
        private IOpl oplA, oplB;
        private short[] lbuf, rbuf;
        private byte[,] iFMReg = new byte[2, 256];
        private byte[,] iTweakedFMReg = new byte[2, 256];
        private byte[,] iCurrentTweakedBlock = new byte[2, 9];  // Current value of the Block in the tweaked OPL chip
        private byte[,] iCurrentFNum = new byte[2, 9];          // Current value of the FNum in the tweaked OPL chip
        private double offset = FreqOffset;                     // User configurable frequency offset for surroundopl
        private int currentChip = 0;
        
        public bool IsStereo => true;

        public SurroundOpl(IOpl a, IOpl b)
        {
            oplA = a;
            oplB = b;
            
            lbuf = new short[BuffSize];
            rbuf = new short[BuffSize];
        }

        public void Init(int rate)
        {
            oplA.Init(rate);
            oplB.Init(rate);
            for (int c = 0; c < 2; c++) {
                for (int i = 0; i < 256; i++) {
                    iFMReg[c, i] = 0;
                    iTweakedFMReg[c, i] = 0;
                }
                for (int i = 0; i < 9; i++) {
                    iCurrentTweakedBlock[c, i] = 0;
                    iCurrentFNum[c, i] = 0;
                }
            }
        }

        public void WriteReg(int reg, int val)
        {
            oplA.WriteReg(reg, val);

			// Transpose the other channel to produce the harmonic effect
			int iChannel = -1;
			int iRegister = reg; // temp
			int iValue = val; // temp
			if ((iRegister >> 4 == 0xA) || (iRegister >> 4 == 0xB))
			{
				iChannel = iRegister & 0x0F;
			}

			// Remember the FM state, so that the harmonic effect can access
			// previously assigned register values.
			iFMReg[currentChip, iRegister] = (byte)iValue;

			if ((iChannel >= 0)) {// && (i == 1)) {
				byte iBlock = (byte)((iFMReg[currentChip, 0xB0 + iChannel] >> 2) & 0x07);
				ushort iFNum = (ushort)(((iFMReg[currentChip, 0xB0 + iChannel] & 0x03) << 8) | iFMReg[currentChip, 0xA0 + iChannel]);
				double dbOriginalFreq = 49716.0 * (double)iFNum * Math.Pow(2.0, iBlock - 20);

				byte iNewBlock = iBlock;
				ushort iNewFNum;

				// Adjust the frequency and calculate the new FNum
				//double dbNewFNum = (dbOriginalFreq+(dbOriginalFreq/FREQ_OFFSET)) / (50000.0 * pow(2, iNewBlock - 20));
				//#define calcFNum() ((dbOriginalFreq+(dbOriginalFreq/FREQ_OFFSET)) / (50000.0 * pow(2, iNewBlock - 20)))
				double dbNewFNum = CalcFNum(dbOriginalFreq, iNewBlock);

				// Make sure it's in range for the OPL chip
				if (dbNewFNum > 1023 - NewBlockLimit) {
					// It's too high, so move up one block (octave) and recalculate

					if (iNewBlock > 6) {
						// Uh oh, we're already at the highest octave!
						//AdPlug_LogWrite("OPL WARN: FNum %d/B#%d would need block 8+ after being transposed (new FNum is %d)\n", iFNum, iBlock, (int)dbNewFNum);
						// The best we can do here is to just play the same note out of the second OPL, so at least it shouldn't
						// sound *too* bad (hopefully it will just miss out on the nice harmonic.)
						iNewBlock = iBlock;
						iNewFNum = iFNum;
					} else {
						iNewBlock++;
						iNewFNum = (ushort)CalcFNum(dbOriginalFreq, iNewBlock);
					}
				} else if (dbNewFNum < 0 + NewBlockLimit) {
					// It's too low, so move down one block (octave) and recalculate

					if (iNewBlock == 0) {
						// Uh oh, we're already at the lowest octave!
						//AdPlug_LogWrite("OPL WARN: FNum %d/B#%d would need block -1 after being transposed (new FNum is %d)!\n", iFNum, iBlock, (int)dbNewFNum);
						// The best we can do here is to just play the same note out of the second OPL, so at least it shouldn't
						// sound *too* bad (hopefully it will just miss out on the nice harmonic.)
						iNewBlock = iBlock;
						iNewFNum = iFNum;
					} else {
						iNewBlock--;
						iNewFNum = (ushort)CalcFNum(dbOriginalFreq, iNewBlock);
					}
				} else {
					// Original calculation is within range, use that
					iNewFNum = (ushort)dbNewFNum;
				}

				// Sanity check
				if (iNewFNum > 1023) {
					// Uh oh, the new FNum is still out of range! (This shouldn't happen)
					//AdPlug_LogWrite("OPL ERR: Original note (FNum %d/B#%d is still out of range after change to FNum %d/B#%d!\n", iFNum, iBlock, iNewFNum, iNewBlock);
					// The best we can do here is to just play the same note out of the second OPL, so at least it shouldn't
					// sound *too* bad (hopefully it will just miss out on the nice harmonic.)
					iNewBlock = iBlock;
					iNewFNum = iFNum;
				}

				if ((iRegister >= 0xB0) && (iRegister <= 0xB8)) {

					// Overwrite the supplied value with the new F-Number and Block.
					iValue = (iValue & ~0x1F) | (iNewBlock << 2) | ((iNewFNum >> 8) & 0x03);

					iCurrentTweakedBlock[currentChip, iChannel] = iNewBlock; // save it so we don't have to update register 0xB0 later on
					iCurrentFNum[currentChip, iChannel] = (byte)iNewFNum;

					if (iTweakedFMReg[currentChip, 0xA0 + iChannel] != (iNewFNum & 0xFF)) {
						// Need to write out low bits
						byte iAdditionalReg = (byte)(0xA0 + iChannel);
						byte iAdditionalValue = (byte)(iNewFNum & 0xFF);
						oplB.WriteReg(iAdditionalReg, iAdditionalValue);
						iTweakedFMReg[currentChip, iAdditionalReg] = iAdditionalValue;
					}
				} else if ((iRegister >= 0xA0) && (iRegister <= 0xA8)) {

					// Overwrite the supplied value with the new F-Number.
					iValue = iNewFNum & 0xFF;

					// See if we need to update the block number, which is stored in a different register
					byte iNewB0Value = (byte)((iFMReg[currentChip, 0xB0 + iChannel] & ~0x1F) | (iNewBlock << 2) | ((iNewFNum >> 8) & 0x03));
					if (
						(iNewB0Value & 0x20) > 0 && // but only update if there's a note currently playing (otherwise we can just wait
						(iTweakedFMReg[currentChip, 0xB0 + iChannel] != iNewB0Value)   // until the next noteon and update it then)
					) {
						//AdPlug_LogWrite("OPL INFO: CH%d - FNum %d/B#%d -> FNum %d/B#%d == keyon register update!\n", iChannel, iFNum, iBlock, iNewFNum, iNewBlock);
						// The note is already playing, so we need to adjust the upper bits too
						byte iAdditionalReg = (byte)(0xB0 + iChannel);
						oplB.WriteReg(iAdditionalReg, iNewB0Value);
						iTweakedFMReg[currentChip, iAdditionalReg] = iNewB0Value;
					} // else the note is not playing, the upper bits will be set when the note is next played

				} // if (register 0xB0 or 0xA0)

			} // if (a register we're interested in)

			// Now write to the original register with a possibly modified value
			oplB.WriteReg(iRegister, iValue);
			iTweakedFMReg[currentChip, iRegister] = (byte)iValue;
        }

        public void ReadBuffer(short[] buffer, int pos, int length)
        {
            oplA.ReadBuffer(lbuf, pos, length);
            oplB.ReadBuffer(rbuf, pos, length);

            // Copy the two mono OPL buffers into the stereo buffer
            int t = 0;
            for (int i = pos; i < (pos + length); i++) {
                int offsetL = i, offsetR = i;
                buffer[i * 2] = lbuf[offsetL];
                buffer[i * 2 + 1] = rbuf[offsetR];
            }
        }
        
        private double CalcFNum(double originalFreq, byte iNewBlock)
        {
	        return (originalFreq + (originalFreq / offset)) / (49716.0 * Math.Pow(2.0, iNewBlock - 20));
        }
    }
}