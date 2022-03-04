﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.StaticPipeline;

namespace InstructionSetProject.Backend.InstructionTypes
{
    public abstract class R3Instruction : IInstruction
    {
        public ushort lengthInBytes => 2;
        public abstract ControlBits controlBits { get; }
        public const ushort BitwiseMask = 0b1111_1110_0000_0000;
        public abstract AluOperation? aluOperation { get; }
        public ushort? destinationRegister { get; set; }
        public ushort? sourceRegister1 { get; set; }
        public ushort? sourceRegister2 { get; set; }
        public ushort? addressingMode { get => null; set { } }
        public ushort? immediate { get => null; set { } }

        public abstract string GetMnemonic();
        public abstract ushort GetOpCode();
        public abstract string Disassemble();
        public abstract void ParseInstruction(string assemblyCode);

        public (ushort opcode, ushort? operand) Assemble()
        {
            var opcode = (ushort)(GetOpCode() | (destinationRegister ?? 0) | (sourceRegister1 ?? 0) | (sourceRegister2 ?? 0));
            return (opcode, null);
        }

        public void ParseInstruction((ushort opcode, ushort? operand) machineCode)
        {
            destinationRegister = (ushort)(machineCode.opcode & 0b111);
            sourceRegister1 = (ushort)(machineCode.opcode & 0b11_1000);
            sourceRegister2 = (ushort)(machineCode.opcode & 0b1_1100_0000);
        }
    }
}
