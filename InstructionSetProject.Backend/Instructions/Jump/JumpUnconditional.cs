﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstructionSetProject.Backend.Instructions.Jump
{
    public class JumpUnconditional : IInstruction
    {
        public const string Mnemonic = "JMP";

        public const ushort OpCode = 0x180;

        public List<byte> Assemble(string assemblyLine)
        {
            throw new NotImplementedException();
        }

        public string Disassemble(List<byte> machineLine)
        {
            throw new NotImplementedException();
        }
    }
}
