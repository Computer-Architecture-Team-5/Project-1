﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.Instructions.F2Types;
using InstructionSetProject.Backend.Instructions.FmTypes;
using InstructionSetProject.Backend.Instructions.R2Types;
using InstructionSetProject.Backend.Instructions.RmTypes;
using InstructionSetProject.Backend.InstructionTypes;

namespace InstructionSetProject.Backend.DynamicPipeline
{
    public class MemoryUnit
    {
        public PipelineDataStructures dataStructures { get; set; }
        public Queue<InstructionInFlight> loadBuffers { get; set; }
        public InstructionInFlight? activeInstruction { get; set; }

        public MemoryUnit(PipelineDataStructures dataStructures)
        {
            this.dataStructures = dataStructures;
            loadBuffers = new();
        }

        public InstructionInFlight? ClockTick()
        {
            activeInstruction ??= GetNextReadyInstruction();

            if (activeInstruction == null)
                return null;

            if (activeInstruction.cyclesRemainingInMemory > 1)
            {
                activeInstruction.cyclesRemainingInMemory--;
                return null;
            }

            if (activeInstruction.instruction.controlBits.MemRead)
            {
                activeInstruction.result = PerformMemRead(activeInstruction);
            }

            var instr = activeInstruction;
            activeInstruction = null;
            return instr;
        }

        private InstructionInFlight? GetNextReadyInstruction()
        {
            if (loadBuffers.Count == 0) return null;
            var nextInstr = loadBuffers.Peek();
            if (nextInstr.StillHasDependencies()) return null;

            if (nextInstr.instruction.controlBits.MemRead)
            {
                return loadBuffers.Dequeue();
            }
            if (nextInstr.instruction.controlBits.MemWrite)
            {
                if (!nextInstr.StillHasDependencies())
                    return loadBuffers.Dequeue();
            }
            else
            {
                throw new Exception("Attempted memory operation that neither reads nor writes.");
            }

            return null;
        }

        private ushort PerformMemRead(InstructionInFlight instr)
        {
            if (instr.instruction is LoadWord || instr.instruction is LoadFloat)
            {
                var readTarget = (instr.instruction.addressingMode == 0b001_0000 || instr.instruction.addressingMode == 0b001_1000) ? instr.lhsValue : instr.instruction.immediate;
                if (readTarget == null || instr.instruction.addressingMode == null)
                    throw new Exception("Null read values");
                return dataStructures.Memory.ReadUshort(readTarget ?? 0, instr.instruction.addressingMode ?? 0);
            }

            if (instr.instruction is PopWord || instr.instruction is PopFloat)
            {
                return dataStructures.Memory.StackPopWord();
            }

            throw new Exception("Unsupported read instruction");
        }
    }
}
