﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.Instructions.F2Types;
using InstructionSetProject.Backend.Instructions.FmTypes;
using InstructionSetProject.Backend.Instructions.R2Types;
using InstructionSetProject.Backend.Instructions.RmTypes;
using InstructionSetProject.Backend.InstructionTypes;
using InstructionSetProject.Backend.Utilities;

namespace InstructionSetProject.Backend.StaticPipeline
{
    public class StaticPipelineExecution : IExecution
    {
        public InstructionList InstrList;
        public PipelineDataStructures DataStructures;
        public List<byte> MachineCode;
        public PipelineStatistics Statistics;

        public Alu Alu;

        public DecodeExecute DecodeExecute;
        public ExecuteMemory ExecuteMemory;
        public MemoryWriteBack MemoryWriteBack;

        private int _fetchStageOffset = 0;
        private int _decodeStageOffset = -1;
        private int _executeStageOffset = -1;
        private int _memoryStageOffset = -1;
        private int _writeBackStageOffset = -1;

        public IInstruction? fetchingInstruction => (_fetchStageOffset >= 0 && _fetchStageOffset <= InstrList.MaxOffset) ? InstrList.GetFromOffset(_fetchStageOffset) : null;
        public IInstruction? decodingInstruction => (_decodeStageOffset >= 0 && _decodeStageOffset <= InstrList.MaxOffset) ? InstrList.GetFromOffset(_decodeStageOffset) : null;
        public IInstruction? executingInstruction => (_executeStageOffset >= 0 && _executeStageOffset <= InstrList.MaxOffset) ? InstrList.GetFromOffset(_executeStageOffset) : null;
        public IInstruction? memoryInstruction => (_memoryStageOffset >= 0 && _memoryStageOffset <= InstrList.MaxOffset) ? InstrList.GetFromOffset(_memoryStageOffset) : null;
        public IInstruction? writingBackInstruction => (_writeBackStageOffset >= 0 && _writeBackStageOffset <= InstrList.MaxOffset) ? InstrList.GetFromOffset(_writeBackStageOffset) : null;

        public IInstruction? nextInstructionToFinish => writingBackInstruction ?? (memoryInstruction ?? (executingInstruction ?? (decodingInstruction ?? (fetchingInstruction ?? null))));

        public StaticPipelineExecution(InstructionList instrList, CacheConfiguration l1Config, CacheConfiguration l2Config)
        {
            DataStructures = new(l1Config, l2Config);
            Alu = new(DataStructures);
            InstrList = instrList;
            MachineCode = Assembler.Assemble(instrList);
            DataStructures.Memory.AddInstructionCode(MachineCode);
            Statistics = new();
        }

        public void Continue()
        {
            while (!IsExecutionFinished())
                Step();
        }

        public bool IsExecutionFinished()
        {
            return !(fetchingInstruction != null || decodingInstruction != null || executingInstruction != null ||
                     memoryInstruction != null || writingBackInstruction != null);
        }

        public void Step()
        {
            while (writingBackInstruction != nextInstructionToFinish)
            {
                ClockTick();
            }
            ClockTick();
        }

        public void ClockTick()
        {
            WriteBack();
            MemoryAccess();
            Execute();
            Decode();
            Fetch();

            Statistics.ClockTicks++;
        }

        public int CyclesRemainingInMemoryStage;
        public int CyclesRemainingInExecuteStage;

        private void Fetch()
        {
            if (_decodeStageOffset != -1) return;
            var instr = fetchingInstruction;
            if (instr == null) return;

            DataStructures.InstructionPointer.value += instr.lengthInBytes;
            _decodeStageOffset = _fetchStageOffset;
            _fetchStageOffset = DataStructures.InstructionPointer.value;
        }

        private void Decode()
        {
            if (_executeStageOffset != -1) return;
            var instr = decodingInstruction;
            if (instr == null)
            {
                DecodeExecute.Immediate = null;
                DecodeExecute.ReadData1 = null;
                DecodeExecute.ReadData2 = null;
                DecodeExecute.ReadReg1 = null;
                DecodeExecute.ReadReg2 = null;
                DecodeExecute.WriteRegister = null;
                return;
            }

            if (instr is IImmediateInstruction immediateInstr)
            {
                DecodeExecute.Immediate = immediateInstr.GenerateImmediate();
            }
            else
            {
                DecodeExecute.Immediate = null;
            }

            var readReg1 = GetFirstReadRegister(instr);
            if (readReg1 != null)
            {
                DecodeExecute.ReadData1 = readReg1.value;
                DecodeExecute.ReadReg1 = readReg1;
            }
            else
            {
                DecodeExecute.ReadData1 = null;
                DecodeExecute.ReadReg1 = null;
            }

            var readReg2 = GetSecondReadRegister(instr);
            if (readReg2 != null)
            {
                DecodeExecute.ReadData2 = readReg2.value;
                DecodeExecute.ReadReg2 = readReg2;
            }
            else
            {
                DecodeExecute.ReadData2 = null;
                DecodeExecute.ReadReg2 = null;
            }

            if (instr is StoreWord || instr is StoreFloat || instr is PushWord || instr is PushFloat)
            {
                (DecodeExecute.ReadData1, DecodeExecute.ReadData2) = (DecodeExecute.ReadData2, DecodeExecute.ReadData1);
                (DecodeExecute.ReadReg1, DecodeExecute.ReadReg2) = (DecodeExecute.ReadReg2, DecodeExecute.ReadReg1);
            }

            DecodeExecute.WriteRegister = GetDestinationRegister(instr);
            _executeStageOffset = _decodeStageOffset;
            CyclesRemainingInExecuteStage = instr.cyclesNeededInExecute;
            _decodeStageOffset = -1;
        }

        private void Execute()
        {
            if (CyclesRemainingInExecuteStage > 1)
            {
                CyclesRemainingInExecuteStage--;
                return;
            }
            if (_memoryStageOffset != -1) return;
            if (writingBackInstruction != null && MemoryWriteBack.WriteRegister != null && MemoryWriteBack.WriteRegister != DataStructures.R0 && (MemoryWriteBack.WriteRegister == DecodeExecute.ReadReg1 ||
                MemoryWriteBack.WriteRegister == DecodeExecute.ReadReg2)) return;
            var instr = executingInstruction;
            if (instr == null)
            {
                ExecuteMemory.ReadData2 = null;
                ExecuteMemory.WriteRegister = null;
                ExecuteMemory.AluResult = null;
                ExecuteMemory.FlagsRegister = null;
                return;
            }

            ExecuteMemory.WriteRegister = DecodeExecute.WriteRegister;

            ushort? aluSource2 = instr.controlBits.ALUSrc ? DecodeExecute.Immediate : DecodeExecute.ReadData2;
            var aluOutput = Alu.Execute((AluOperation)instr.aluOperation, DecodeExecute.ReadData1, aluSource2);
            ExecuteMemory.AluResult = aluOutput.result;
            ExecuteMemory.FlagsRegister = aluOutput.flags;
            if (instr.controlBits.UpdateFlags)
                DataStructures.Flags = aluOutput.flags;

            ExecuteMemory.ReadData2 = DecodeExecute.ReadData2;

            ExecuteMemory.Immediate = DecodeExecute.Immediate;

            _memoryStageOffset = _executeStageOffset;
            SetCyclesRemainingInMemoryStage(instr);
            _executeStageOffset = -1;
            DecodeExecute = new();
        }

        private void SetCyclesRemainingInMemoryStage(IInstruction instr)
        {
            if (instr is LoadWord || instr is StoreWord)
            {
                if (ExecuteMemory.AluResult == null || instr.addressingMode == null)
                    throw new Exception("Invalid memory access");

                var rand = new Random();
                var address =
                    DataStructures.AddressResolver.GetAddress(ExecuteMemory.AluResult ?? 0, instr.addressingMode ?? 0);
                if (DataStructures.L1.GetCacheSet(address).IsDataPresent(address, 2))
                {
                    CyclesRemainingInMemoryStage = rand.Next(1, 5);
                }

                else if (DataStructures.L2.GetCacheSet(address).IsDataPresent(address, 2))
                {
                    CyclesRemainingInMemoryStage = rand.Next(20, 50);
                }

                else
                {
                    CyclesRemainingInMemoryStage = rand.Next(100, 500);
                }
            }
            else
            {
                CyclesRemainingInMemoryStage = instr.cyclesNeededInMemory;
            }
        }

        private void MemoryAccess()
        {
            if (CyclesRemainingInMemoryStage > 1)
            {
                CyclesRemainingInMemoryStage--;
                return;
            }
            var instr = memoryInstruction;
            if (instr == null)
            {
                MemoryWriteBack.WriteRegister = null;
                MemoryWriteBack.AluResult = null;
                MemoryWriteBack.ReadData = null;
                return;
            }

            MemoryWriteBack.WriteRegister = ExecuteMemory.WriteRegister;

            MemoryWriteBack.AluResult = ExecuteMemory.AluResult;

            if (instr.controlBits.MemRead)
            {
                MemoryWriteBack.ReadData = PerformMemRead(instr);
            }
            else if (instr.controlBits.MemWrite)
            {
                PerformMemWrite(instr);
                MemoryWriteBack.ReadData = null;
            }
            else
            {
                MemoryWriteBack.ReadData = null;
            }

            if (instr.controlBits.PCSrc && ExecuteMemory.Immediate != null)
            {
                if (instr is IFlagInstruction flagInstr)
                {
                    if (ExecuteMemory.FlagsRegister != null && ExecuteMemory.FlagsRegister?.IsFlagSet(flagInstr.flagToCheck) == flagInstr.flagEnabled)
                    {
                        DataStructures.InstructionPointer.value = (ushort)ExecuteMemory.Immediate;
                        FlushPipeline();
                    }
                }
                else
                {
                    DataStructures.InstructionPointer.value = (ushort)ExecuteMemory.Immediate;
                    FlushPipeline();
                }
            }

            _writeBackStageOffset = _memoryStageOffset;
            _memoryStageOffset = -1;
            ExecuteMemory = new();
        }

        private void WriteBack()
        {
            var instr = writingBackInstruction;
            if (instr == null) return;

            if (instr.controlBits.RegWrite && MemoryWriteBack.WriteRegister != DataStructures.R0)
            {
                if (MemoryWriteBack.WriteRegister == null)
                {
                    throw new Exception("Attempt to write to null register");
                }

                if (instr.controlBits.MemToReg)
                {
                    if (MemoryWriteBack.ReadData == null)
                    {
                        throw new Exception("Attempt to write null value to register");
                    }

                    MemoryWriteBack.WriteRegister.value = (ushort)MemoryWriteBack.ReadData;
                }
                else
                {
                    if (MemoryWriteBack.AluResult == null)
                    {
                        throw new Exception("Attempt to write null value to register");
                    }

                    MemoryWriteBack.WriteRegister.value = (ushort)MemoryWriteBack.AluResult;
                    ForwardNewRegisterValue(MemoryWriteBack.WriteRegister);
                }
            }
            Statistics.StatInstructionType(writingBackInstruction);
            _writeBackStageOffset = -1;
            MemoryWriteBack = new();
        }

        private ushort PerformMemRead(IInstruction instr)
        {
            if (instr is LoadWord || instr is LoadFloat)
            {
                if (ExecuteMemory.AluResult == null || instr.addressingMode == null)
                    throw new Exception("Null read values");
                return DataStructures.L1.ReadUshort(DataStructures.AddressResolver.GetAddress(ExecuteMemory.AluResult ?? 0, instr.addressingMode ?? 0));
            }

            if (instr is PopWord || instr is PopFloat)
            {
                return DataStructures.Memory.StackPopWord();
            }

            throw new Exception("Unsupported read instruction");
        }

        private void PerformMemWrite(IInstruction instr)
        {
            if (instr is StoreWord || instr is StoreFloat)
            {
                if (ExecuteMemory.AluResult == null || instr.addressingMode == null || ExecuteMemory.ReadData2 == null)
                    throw new Exception("Null write values");
                DataStructures.L1.WriteUshort(DataStructures.AddressResolver.GetAddress(ExecuteMemory.AluResult ?? 0, instr.addressingMode ?? 0), ExecuteMemory.ReadData2 ?? 0);
                return;
            }

            if (instr is PushWord || instr is PushFloat)
            {
                DataStructures.Memory.StackPushWord(ExecuteMemory.ReadData2 ?? 0);
                return;
            }

            throw new Exception("Unsupported write instruction");
        }

        private void FlushPipeline()
        {
            _executeStageOffset = -1;
            _decodeStageOffset = -1;
            _fetchStageOffset = DataStructures.InstructionPointer.value;
            Statistics.FlushCount++;
        }

        private void ForwardNewRegisterValue(Register<ushort> writeReg)
        {
            if (DecodeExecute.ReadReg1 == writeReg)
                DecodeExecute.ReadData1 = writeReg.value;

            if (DecodeExecute.ReadReg2 == writeReg)
                DecodeExecute.ReadData2 = writeReg.value;
        }

        private Register<ushort>? GetDestinationRegister(IInstruction instr)
        {
            if (instr.firstRegisterType == RegisterType.Write)
                return GetFirstRegister(instr);
            return null;
        }

        private Register<ushort>? GetFirstReadRegister(IInstruction instr)
        {
            if (instr.firstRegisterType == RegisterType.Read)
                return GetFirstRegister(instr);
            if (instr.secondRegisterType == RegisterType.Read)
                return GetSecondRegister(instr);
            if (instr is IImmediateRegister &&
                (instr.addressingMode == 0b001_0000 || instr.addressingMode == 0b001_1000))
                return GetImmediateRegister(instr);
            return null;
        }

        private Register<ushort>? GetSecondReadRegister(IInstruction instr)
        {
            if (instr.firstRegisterType == RegisterType.Read && instr.secondRegisterType == RegisterType.Read)
                return GetSecondRegister(instr);
            if (instr.thirdRegisterType == RegisterType.Read)
                return GetThirdRegister(instr);
            if (instr is IImmediateRegister && instr.firstRegisterType == RegisterType.Read &&
                (instr.addressingMode == 0b001_0000 || instr.addressingMode == 0b001_1000))
                return GetImmediateRegister(instr);
            return null;
        }

        private Register<ushort>? GetFirstRegister(IInstruction instr)
        {
            if (instr is F2Instruction f2Instr)
                return ConvertRegisterIndexToFloatRegister(f2Instr.firstRegister ?? 0);
            if (instr is R2Instruction r2Instr)
                return ConvertRegisterIndexToIntRegister(r2Instr.firstRegister ?? 0);
            if (instr is F3Instruction f3Instr)
                return ConvertRegisterIndexToFloatRegister(f3Instr.firstRegister ?? 0);
            if (instr is R3Instruction r3Instr)
                return ConvertRegisterIndexToIntRegister(r3Instr.firstRegister ?? 0);
            if (instr is RsInstruction rsInstr)
                return ConvertRegisterIndexToIntRegister(rsInstr.firstRegister ?? 0);
            if (instr is RmInstruction rmInstr)
                return ConvertRegisterIndexToIntRegister(rmInstr.firstRegister ?? 0);
            if (instr is FmInstruction fmInstr)
                return ConvertRegisterIndexToFloatRegister(fmInstr.firstRegister ?? 0);
            return null;
        }

        private Register<ushort>? GetSecondRegister(IInstruction instr)
        {
            if (instr is F2Instruction f2Instr)
                return ConvertRegisterIndexToFloatRegister((ushort)((f2Instr.secondRegister ?? 0) >> 3));
            if (instr is R2Instruction r2Instr)
                return ConvertRegisterIndexToIntRegister((ushort)((r2Instr.secondRegister ?? 0) >> 3));
            if (instr is F3Instruction f3Instr)
                return ConvertRegisterIndexToFloatRegister((ushort)((f3Instr.secondRegister ?? 0) >> 3));
            if (instr is R3Instruction r3Instr)
                return ConvertRegisterIndexToIntRegister((ushort)((r3Instr.secondRegister ?? 0) >> 3));
            if (instr is RsInstruction rsInstr)
                return ConvertRegisterIndexToIntRegister((ushort)((rsInstr.secondRegister ?? 0) >> 3));
            if (instr is RmInstruction rmInstr)
                return ConvertRegisterIndexToIntRegister((ushort)((rmInstr.secondRegister ?? 0) >> 3));
            if (instr is FmInstruction fmInstr)
                return ConvertRegisterIndexToFloatRegister((ushort)((fmInstr.secondRegister ?? 0) >> 3));
            return null;
        }

        private Register<ushort>? GetThirdRegister(IInstruction instr)
        {
            if (instr is R3Instruction r3Instr)
                return ConvertRegisterIndexToIntRegister((ushort)((r3Instr.thirdRegister ?? 0) >> 6));
            if (instr is F3Instruction f3Instr)
                return ConvertRegisterIndexToFloatRegister((ushort)((f3Instr.thirdRegister ?? 0) >> 6));
            return null;
        }

        private Register<ushort>? GetImmediateRegister(IInstruction instr)
        {
            if (instr is RmInstruction rmInstr)
                return ConvertRegisterIndexToIntRegister(rmInstr.immediate ?? 0);
            if (instr is FmInstruction fmInstr)
                return ConvertRegisterIndexToFloatRegister(fmInstr.immediate ?? 0);
            return null;
        }

        private Register<ushort> ConvertRegisterIndexToIntRegister(ushort index)
        {
            switch (index)
            {
                case 1:
                    return DataStructures.R1;
                case 2:
                    return DataStructures.R2;
                case 3:
                    return DataStructures.R3;
                case 4:
                    return DataStructures.R4;
                case 5:
                    return DataStructures.R5;
                case 6:
                    return DataStructures.R6;
                case 7:
                    return DataStructures.R7;
                case 8:
                    return DataStructures.MemoryBasePointer;
                default:
                    return DataStructures.R0;
            }
        }

        private Register<ushort> ConvertRegisterIndexToFloatRegister(ushort index)
        {
            switch (index)
            {
                case 1:
                    return DataStructures.F1;
                case 2:
                    return DataStructures.F2;
                case 3:
                    return DataStructures.F3;
                case 4:
                    return DataStructures.F4;
                case 5:
                    return DataStructures.F5;
                case 6:
                    return DataStructures.F6;
                case 7:
                    return DataStructures.F7;
                default:
                    return DataStructures.F0;
            }
        }
    }

    public struct DecodeExecute
    {
        public ushort? ReadData1;
        public Register<ushort>? ReadReg1;
        public ushort? ReadData2;
        public Register<ushort>? ReadReg2;
        public ushort? Immediate;
        public Register<ushort>? WriteRegister;
    }

    public struct ExecuteMemory
    {
        public ushort? AluResult;
        public ushort? ReadData2;
        public ushort? Immediate;
        public Register<ushort>? WriteRegister;
        public FlagsRegister? FlagsRegister;
    }

    public struct MemoryWriteBack
    {
        public ushort? ReadData;
        public ushort? AluResult;
        public Register<ushort>? WriteRegister;
    }
}
