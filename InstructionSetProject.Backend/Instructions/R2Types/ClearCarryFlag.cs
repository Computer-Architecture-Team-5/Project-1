﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.InstructionTypes.R2Formats;
using InstructionSetProject.Backend.StaticPipeline;

namespace InstructionSetProject.Backend.Instructions.R2Types
{
    public class ClearCarryFlag : R2NoOperands
    {
        public const string Mnemonic = "CLC";

        public const ushort OpCode = 0b0000_0110_1100_0000;

        public override ControlBits controlBits => new(false, false, false, false, false, false, false);

        public override AluOperation? aluOperation => AluOperation.ClearCarryFlag;

        public override string GetMnemonic()
        {
            return Mnemonic;
        }

        public override ushort GetOpCode()
        {
            return OpCode;
        }
    }
}
