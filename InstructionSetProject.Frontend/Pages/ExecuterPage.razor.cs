﻿using InstructionSetProject.Backend;
using InstructionSetProject.Backend.InstructionTypes;
using InstructionSetProject.Backend.StaticFrontend;
using InstructionSetProject.Backend.StaticPipeline;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Syncfusion.Blazor.Diagram;
using Syncfusion.Blazor.Popups;
using DiagramSegments = Syncfusion.Blazor.Diagram.ConnectorSegmentType;

namespace InstructionSetProject.Frontend.Pages
{
    public partial class ExecuterPage
    {
        private string ExecMachineCode = "";
        private string ExecAssemblyCode = "";
        private string statsString = "";
        public string MemDumpStart { get; set; } = "";

        public string MemDumpContent => SPEx != null
            ? String.Join(" ",
                SPEx.DataStructures.Memory
                    .GetBytesAtAddress(MemDumpStart != string.Empty ? Convert.ToUInt32(MemDumpStart, 16) : 0)
                    .Select((memByte) => memByte.ToString("X2")))
            : "";

        private StaticPipelineExecution? SPEx;

        public byte[]? MemoryBytes => SPEx != null ? SPEx.DataStructures.Memory.Bytes : null;

        public string r0 => SPEx != null ? SPEx.DataStructures.R0.value.ToString("X4") : "0000";
        public string r1 => SPEx != null ? SPEx.DataStructures.R1.value.ToString("X4") : "0000";
        public string r2 => SPEx != null ? SPEx.DataStructures.R2.value.ToString("X4") : "0000";
        public string r3 => SPEx != null ? SPEx.DataStructures.R3.value.ToString("X4") : "0000";
        public string r4 => SPEx != null ? SPEx.DataStructures.R4.value.ToString("X4") : "0000";
        public string r5 => SPEx != null ? SPEx.DataStructures.R5.value.ToString("X4") : "0000";
        public string r6 => SPEx != null ? SPEx.DataStructures.R6.value.ToString("X4") : "0000";
        public string r7 => SPEx != null ? SPEx.DataStructures.R7.value.ToString("X4") : "0000";
        public string IP => SPEx != null ? SPEx.DataStructures.InstructionPointer.value.ToString("X4") : "0000";
        public string SP => SPEx != null ? SPEx.DataStructures.StackPointer.value.ToString("X4") : "0000";
        public string FL => SPEx != null ? SPEx.DataStructures.Flags.AsRegisterValue().ToString("X4") : "0000";
        public string PC => SPEx != null ? SPEx.DataStructures.InstructionPointer.value.ToString("X4") : "0000";

        private bool debugRender = false;

        private int connectorCount = 0;
        // Reference to diagram
        SfDiagramComponent diagram;
        // Defines diagram's nodes collection
        public DiagramObjectCollection<Node> NodeCollection { get; set; }
        // Defines diagram's connector collection
        public DiagramObjectCollection<Connector> ConnectorCollection { get; set; }


        private List<byte> machineCode = new();
        private string output { get; set; }
        private string machineCodeString { get; set; }

        private string fileContent = "";

        public bool ShowItem { get; set; } = true;
        private bool Visibility { get; set; } = false;
        private bool errorVis { get; set; } = false;
        private bool ShowButton { get; set; } = false;
        private ResizeDirection[] dialogResizeDirections { get; set; } = new ResizeDirection[] { ResizeDirection.All };

        private string StartMemDumpLimit()
        {
            return "Need to implement";
        }

        private async Task LoadFile(InputFileChangeEventArgs e)
        {
            var file = e.File;
            long maxsize = 512000;

            var buffer = new byte[file.Size];
            await file.OpenReadStream(maxsize).ReadAsync(buffer);
            fileContent = System.Text.Encoding.UTF8.GetString(buffer);
            ExecAssemblyCode = fileContent;
        }

        private async Task SaveAssemblyCode()
        {
            byte[] file = System.Text.Encoding.UTF8.GetBytes(ExecAssemblyCode);
            await JSRuntime.InvokeVoidAsync("BlazorDownloadFile", "assemblyCode.txt", "text/plain", file);
        }
        private async Task SaveStats()
        {
            byte[] file = System.Text.Encoding.UTF8.GetBytes(statsString);
            await JSRuntime.InvokeVoidAsync("BlazorDownloadFile", "assemblyStats.txt", "text/plain", file);
        }

        public string Statistics()
        {
            statsString = "";
            if (SPEx == null) return "No Statistics Yet";
            statsString += "Instruction Types\n";
            statsString += "-----------------\n";
            statsString += "R2 Type: " + SPEx.Statistics.R2InstructionCount + "\n";
            statsString += "R3 Type: " + SPEx.Statistics.R3InstructionCount + "\n";
            statsString += "Rm Type: " + SPEx.Statistics.RmInstructionCount + "\n";
            statsString += "Rs Type: " + SPEx.Statistics.RsInstructionCount + "\n";
            statsString += "F2 Type: " + SPEx.Statistics.F2InstructionCount + "\n";
            statsString += "F3 Type: " + SPEx.Statistics.F3InstructionCount + "\n";
            statsString += "Fm Type: " + SPEx.Statistics.FmInstructionCount + "\n\n";

            statsString += "Clock\n";
            statsString += "-----\n";
            statsString += "Total Clock Ticks: " + SPEx.Statistics.ClockTicks + "\n\n";

            statsString += "Flush\n";
            statsString += "-----\n";
            statsString += "Total Flushes: " + SPEx.Statistics.FlushCount;

            return statsString;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (debugRender)
            {
                await JSRuntime.InvokeVoidAsync("selectDebugTab");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("autoSelectFirstTab");
            }
            debugRender = false;
        }

        protected override async Task OnInitializedAsync()
        {
            StartupMethod();
            Statistics();
        }

        void StartupMethod()
        {
            ExecMachineCode = FrontendVariables.currentMachineCodeExecuter;
            FrontendVariables.currentMachineCodeExecuter = "";
            ExecAssemblyCode = FrontendVariables.currentAssemblyCodeExecuter;
            FrontendVariables.currentAssemblyCodeExecuter = "";
        }

        private static List<byte> HexStringToByteList(string machineCodeString)
        {
            if (machineCodeString.Length % 2 == 1)
                throw new Exception("Cannot have an odd number of digits!!");

            int numChars = machineCodeString.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
                bytes[i / 2] = Convert.ToByte(machineCodeString.Substring(i, 2), 16);

            List<byte> mCode = new List<byte>(bytes);

            return mCode;
        }

        void buildCode()
        {
            if (ExecAssemblyCode.Length != 0)
            {
                machineCode = Assembler.Assemble(ExecAssemblyCode);
                string hexCode = BitConverter.ToString(machineCode.ToArray());
                ExecMachineCode = hexCode.Replace("-", " ");
            }
            else
            {
                errorVis = true;
            }
        }

        void runCode()
        {
            SPEx = (StaticPipelineExecution)StaticPipelineExecutor.Execute(ExecAssemblyCode);
            SPEx.Continue();
            Statistics();
        }

        void Debug()
        {
            SPEx = (StaticPipelineExecution)StaticPipelineExecutor.Execute(ExecAssemblyCode);
            OnItemClick();
            debugRender = true;
            JSRuntime.InvokeVoidAsync("debugScrollToTop");
        }

        bool IsSelectedFetch(IInstruction instr) => instr == SPEx.fetchingInstruction;
        bool IsSelectedDecode(IInstruction instr) => instr == SPEx.decodingInstruction;
        bool IsSelectedExecute(IInstruction instr) => instr == SPEx.executingInstruction;
        bool IsSelectedMemory(IInstruction instr) => instr == SPEx.memoryInstruction;
        bool IsSelectedWrite(IInstruction instr) => instr == SPEx.writingBackInstruction;

        string DivCSS(IInstruction instr) => IsSelectedFetch(instr) ? "bg-fetch text-white" : (IsSelectedDecode(instr) ? "bg-decode text-white" : (IsSelectedExecute(instr) ? "bg-execute text-white" : (IsSelectedMemory(instr) ? "bg-memory text-white" : (IsSelectedWrite(instr) ? "bg-write text-white" : "bg-white"))));

        void ClockTick()
        {
            debugRender = true;
            SPEx.ClockTick();
            JSRuntime.InvokeVoidAsync("stepScroll");
            Statistics();
            debugRender = true;
            UpdateDiagram();
        }

        void step()
        {
            debugRender = true;
            SPEx.Step();
            JSRuntime.InvokeVoidAsync("stepScroll");
            Statistics();
            debugRender = true;
            UpdateDiagram();
        }

        void Continue()
        {
            SPEx.Continue();
            Statistics();
        }

        void UpdateDiagram()
        {
            if (SPEx != null)
            {
                // Updates for Decoding Instruction RegWrite
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.RegWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToRW).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToRW).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToRW).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToRW).Style.StrokeWidth = 1;
                }

                // Updates for decoding Instruction ALUSrc
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.ALUSrc == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToALUS).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToALUS).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToALUS).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToALUS).Style.StrokeWidth = 1;
                }

                // Updates for decoding Instruction MemRead
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.MemRead == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMR).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToMR).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMR).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToMR).Style.StrokeWidth = 1;
                }

                // Updates for decoding Instruction MemWrite
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.MemWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMW).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToMW).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMW).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToMW).Style.StrokeWidth = 1;
                }

                // Updates for decoding Instruction MemToReg
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.MemToReg == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMTR).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToMTR).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToMTR).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToMTR).Style.StrokeWidth = 1;
                }

                // Updates for decoding Instruction PCSrc
                if (SPEx.decodingInstruction != null && SPEx.decodingInstruction.controlBits != null && SPEx.decodingInstruction.controlBits.PCSrc == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToPCS).Style.StrokeColor = "#0004FF";
                    ConnectorCollection.First(connector => connector.ID == ControlToPCS).Style.StrokeWidth = 3;
                }
                else if (SPEx.decodingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ControlToPCS).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ControlToPCS).Style.StrokeWidth = 1;
                }



                // Updates for Executing Instruction RegWrite
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.RegWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == RWToRW1).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == RWToRW1).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == RWToRW1).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == RWToRW1).Style.StrokeWidth = 1;
                }

                // Updates for Executing Instruction ALUSrc
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.ALUSrc == true)
                {
                    ConnectorCollection.First(connector => connector.ID == ALUSToExecuteMux).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == ALUSToExecuteMux).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == ALUSToExecuteMux).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == ALUSToExecuteMux).Style.StrokeWidth = 1;
                }

                // Updates for Executing Instruction MemRead
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.MemRead == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MRToMR1).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == MRToMR1).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MRToMR1).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MRToMR1).Style.StrokeWidth = 1;
                }

                // Updates for Executing Instruction MemWrite
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.MemWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MWToMW1).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == MWToMW1).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MWToMW1).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MWToMW1).Style.StrokeWidth = 1;
                }

                // Updates for Executing Instruction MemToReg
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.MemToReg == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MTRToMTR1).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == MTRToMTR1).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MTRToMTR1).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MTRToMTR1).Style.StrokeWidth = 1;
                }

                // Updates for Executing Instruction PCSrc
                if (SPEx.executingInstruction != null && SPEx.executingInstruction.controlBits != null && SPEx.executingInstruction.controlBits.PCSrc == true)
                {
                    ConnectorCollection.First(connector => connector.ID == PCSToPCS1).Style.StrokeColor = "#0099FF";
                    ConnectorCollection.First(connector => connector.ID == PCSToPCS1).Style.StrokeWidth = 3;
                }
                else if (SPEx.executingInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == PCSToPCS1).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == PCSToPCS1).Style.StrokeWidth = 1;
                }



                // Updates for Memory Instruction RegWrite
                if (SPEx.memoryInstruction != null && SPEx.memoryInstruction.controlBits != null && SPEx.memoryInstruction.controlBits.RegWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == RW1ToRW2).Style.StrokeColor = "#00CC44";
                    ConnectorCollection.First(connector => connector.ID == RW1ToRW2).Style.StrokeWidth = 3;
                }
                else if (SPEx.memoryInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == RW1ToRW2).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == RW1ToRW2).Style.StrokeWidth = 1;
                }

                // Updates for Memory Instruction MemToReg
                if (SPEx.memoryInstruction != null && SPEx.memoryInstruction.controlBits != null && SPEx.memoryInstruction.controlBits.MemToReg == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MTR1ToMTR2).Style.StrokeColor = "#00CC44";
                    ConnectorCollection.First(connector => connector.ID == MTR1ToMTR2).Style.StrokeWidth = 3;
                }
                else if (SPEx.memoryInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MTR1ToMTR2).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MTR1ToMTR2).Style.StrokeWidth = 1;
                }

                // Updates for Memory Instruction MemRead
                if (SPEx.memoryInstruction != null && SPEx.memoryInstruction.controlBits != null && SPEx.memoryInstruction.controlBits.MemRead == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MR1ToDataMem).Style.StrokeColor = "#00CC44";
                    ConnectorCollection.First(connector => connector.ID == MR1ToDataMem).Style.StrokeWidth = 3;
                }
                else if (SPEx.memoryInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MR1ToDataMem).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MR1ToDataMem).Style.StrokeWidth = 1;
                }

                // Updates for Memory Instruction MemWrite
                if (SPEx.memoryInstruction != null && SPEx.memoryInstruction.controlBits != null && SPEx.memoryInstruction.controlBits.MemWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MW1ToDataMem).Style.StrokeColor = "#00CC44";
                    ConnectorCollection.First(connector => connector.ID == MW1ToDataMem).Style.StrokeWidth = 3;
                }
                else if (SPEx.memoryInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MW1ToDataMem).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MW1ToDataMem).Style.StrokeWidth = 1;
                }

                // Updates for Memory Instruction PCSrc
                if (SPEx.memoryInstruction != null && SPEx.memoryInstruction.controlBits != null && SPEx.memoryInstruction.controlBits.PCSrc == true)
                {
                    ConnectorCollection.First(connector => connector.ID == PCS1ToCheckFlags).Style.StrokeColor = "#00CC44";
                    ConnectorCollection.First(connector => connector.ID == PCS1ToCheckFlags).Style.StrokeWidth = 3;
                }
                else if (SPEx.memoryInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == PCS1ToCheckFlags).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == PCS1ToCheckFlags).Style.StrokeWidth = 1;
                }



                // Updates for WriteBack Instruction RegWrite
                if (SPEx.writingBackInstruction != null && SPEx.writingBackInstruction.controlBits != null && SPEx.writingBackInstruction.controlBits.RegWrite == true)
                {
                    ConnectorCollection.First(connector => connector.ID == RW2ToRWRet).Style.StrokeColor = "#B6BF02";
                    ConnectorCollection.First(connector => connector.ID == RW2ToRWRet).Style.StrokeWidth = 3;
                    ConnectorCollection.First(connector => connector.ID == RWRetToReg).Style.StrokeColor = "#B6BF02";
                    ConnectorCollection.First(connector => connector.ID == RWRetToReg).Style.StrokeWidth = 3;
                }
                else if (SPEx.writingBackInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == RW2ToRWRet).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == RW2ToRWRet).Style.StrokeWidth = 1;
                    ConnectorCollection.First(connector => connector.ID == RWRetToReg).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == RWRetToReg).Style.StrokeWidth = 1;
                }

                // Updates for Memory Instruction MemToReg
                if (SPEx.writingBackInstruction != null && SPEx.writingBackInstruction.controlBits != null && SPEx.writingBackInstruction.controlBits.MemToReg == true)
                {
                    ConnectorCollection.First(connector => connector.ID == MTR2ToWriteMux).Style.StrokeColor = "#B6BF02";
                    ConnectorCollection.First(connector => connector.ID == MTR2ToWriteMux).Style.StrokeWidth = 3;
                }
                else if (SPEx.writingBackInstruction == null && ConnectorCollection != null)
                {
                    ConnectorCollection.First(connector => connector.ID == MTR2ToWriteMux).Style.StrokeColor = "black";
                    ConnectorCollection.First(connector => connector.ID == MTR2ToWriteMux).Style.StrokeWidth = 1;
                }
            }
        }

        private void InitDiagramModel()
        {
            NodeCollection = new DiagramObjectCollection<Node>();
            ConnectorCollection = new DiagramObjectCollection<Connector>();

            #region Ports
            // Fetch Ports
            List<PointPort> FetchMuxPorts = new List<PointPort>();
            FetchMuxPorts.Add(AddPort("portFetchMuxIn0", 0.15, 0.01));
            FetchMuxPorts.Add(AddPort("portFetchMuxIn1", 0.85, 0.01));
            FetchMuxPorts.Add(AddPort("portFetchMuxIn2", 1, 0.5));
            FetchMuxPorts.Add(AddPort("portFetchMuxOut0", 0.5, 1));
            List<PointPort> PCPorts = new List<PointPort>();
            PCPorts.Add(AddPort("portPCIn", 0.01, 0.5));
            PCPorts.Add(AddPort("portPCOut", 1, 0.5));
            List<PointPort> InstrMemPorts = new List<PointPort>();
            InstrMemPorts.Add(AddPort("portInstrMemIn", 0.01, 0.1));
            InstrMemPorts.Add(AddPort("portInstrMemOut", 1, 0.5));
            InstrMemPorts.Add(AddPort("portInstrMemOut1", 0.7, 0.01));
            List<PointPort> AddPCPorts = new List<PointPort>();
            AddPCPorts.Add(AddPort("portAddPCIn0", 0.15, 0.01));
            AddPCPorts.Add(AddPort("portAddPCIn1", 0.85, 0.01));
            AddPCPorts.Add(AddPort("portAddPCOut0", 0.5, 1));

            List<PointPort> ifidPorts = new List<PointPort>();
            //ifidPorts.Add(AddPort("portIfidIn0", 0.01, 0.15));
            ifidPorts.Add(AddPort("portIfidIn1", 0.01, 0.5));
            //ifidPorts.Add(AddPort("portIfidOut0", 1, 0.15));
            ifidPorts.Add(AddPort("portIfidOut1", 1, 0.5));

            // Decode Ports
            List<PointPort> regPorts = new List<PointPort>();
            regPorts.Add(AddPort("portRegIn0", 0.01, 0.1));
            regPorts.Add(AddPort("portRegIn1", 0.01, 0.35));
            regPorts.Add(AddPort("portRegIn2", 0.01, 0.7));
            regPorts.Add(AddPort("portRegIn3", 0.1, 1));
            regPorts.Add(AddPort("portRegIn4", 0.4, 0.01));
            regPorts.Add(AddPort("portRegOut0", 1, 0.15));
            regPorts.Add(AddPort("portRegOut1", 1, 0.5));
            List<PointPort> ImmGenPorts = new List<PointPort>();
            ImmGenPorts.Add(AddPort("portImmGenIn", 0.01, 0.5));
            ImmGenPorts.Add(AddPort("portImmGenOut", 1, 0.5));
            List<PointPort> ControlPorts = new List<PointPort>();
            ControlPorts.Add(AddPort("portControlIn", 0.01, 0.5));
            ControlPorts.Add(AddPort("portControlOut1", 1, 0.5));

            List<PointPort> idexPorts = new List<PointPort>();
            //idexPorts.Add(AddPort("portIdexIn0", 0.01, 0.15));
            idexPorts.Add(AddPort("portIdexIn1", 0.01, 0.438));
            idexPorts.Add(AddPort("portIdexIn2", 0.01, 0.515));
            idexPorts.Add(AddPort("portIdexIn3", 0.01, 0.738));
            idexPorts.Add(AddPort("portIdexIn4", 0.01, 0.87));
            //idexPorts.Add(AddPort("portIdexOut0", 1, 0.15));
            idexPorts.Add(AddPort("portIdexOut1", 1, 0.438));
            idexPorts.Add(AddPort("portIdexOut2", 1, 0.515));
            idexPorts.Add(AddPort("portIdexOut3", 1, 0.738));
            idexPorts.Add(AddPort("portIdexOut4", 1, 0.87));
            List<PointPort> RWPorts = new List<PointPort>();
            RWPorts.Add(AddPort("portRWIn", 0.01, 0.5));
            RWPorts.Add(AddPort("portRWOut", 1, 0.5));
            List<PointPort> MTRPorts = new List<PointPort>();
            MTRPorts.Add(AddPort("portMTRIn", 0.01, 0.5));
            MTRPorts.Add(AddPort("portMTROut", 1, 0.5));
            List<PointPort> MRPorts = new List<PointPort>();
            MRPorts.Add(AddPort("portMRIn", 0.01, 0.5));
            MRPorts.Add(AddPort("portMROut", 1, 0.5));
            List<PointPort> MWPorts = new List<PointPort>();
            MWPorts.Add(AddPort("portMWIn", 0.01, 0.5));
            MWPorts.Add(AddPort("portMWOut", 1, 0.5));
            List<PointPort> PCSPorts = new List<PointPort>();
            PCSPorts.Add(AddPort("portPCSIn", 0.01, 0.5));
            PCSPorts.Add(AddPort("portPCSOut", 1, 0.5));
            List<PointPort> ALUSPorts = new List<PointPort>();
            ALUSPorts.Add(AddPort("portALUSIn", 0.01, 0.5));
            ALUSPorts.Add(AddPort("portALUSOut", 1, 0.5));

            // Execute Ports
            List<PointPort> ExecuteMuxPorts = new List<PointPort>();
            ExecuteMuxPorts.Add(AddPort("portExecuteMuxIn0", 0, 0.5));
            ExecuteMuxPorts.Add(AddPort("portExecuteMuxIn1", 0.85, 0.01));
            ExecuteMuxPorts.Add(AddPort("portExecuteMuxIn2", 1, 0.5));
            ExecuteMuxPorts.Add(AddPort("portExecuteMuxOut0", 0.5, 1));
            List<PointPort> AddSumPorts = new List<PointPort>();
            AddSumPorts.Add(AddPort("portAddSumIn0", 0.15, 0.01));
            AddSumPorts.Add(AddPort("portAddSumIn1", 0.85, 0.01));
            AddSumPorts.Add(AddPort("portAddSumOut", 0.5, 1));
            List<PointPort> ALUPorts = new List<PointPort>();
            ALUPorts.Add(AddPort("portALUIn0", 0.14, 0.01));
            ALUPorts.Add(AddPort("portALUIn1", 0.86, 0.01));
            ALUPorts.Add(AddPort("portALUOut0", 0.33, 1));
            ALUPorts.Add(AddPort("portALUOut1", 0.65, 1));

            List<PointPort> exmemPorts = new List<PointPort>();
            exmemPorts.Add(AddPort("portExmemIn0", 0.01, 0.16));
            exmemPorts.Add(AddPort("portExmemIn1", 0.01, 0.39));
            exmemPorts.Add(AddPort("portExmemIn2", 0.01, 0.738));
            exmemPorts.Add(AddPort("portExmemIn3", 0.01, 0.87));
            exmemPorts.Add(AddPort("portExmemOut0", 1, 0.16));
            exmemPorts.Add(AddPort("portExmemOut1", 1, 0.39));
            exmemPorts.Add(AddPort("portExmemOut2", 1, 0.738));
            exmemPorts.Add(AddPort("portExmemOut3", 1, 0.87));
            List<PointPort> RW1Ports = new List<PointPort>();
            RW1Ports.Add(AddPort("portRW1In", 0.01, 0.5));
            RW1Ports.Add(AddPort("portRW1Out", 1, 0.5));
            List<PointPort> MTR1Ports = new List<PointPort>();
            MTR1Ports.Add(AddPort("portMTR1In", 0.01, 0.5));
            MTR1Ports.Add(AddPort("portMTR1Out", 1, 0.5));
            List<PointPort> MR1Ports = new List<PointPort>();
            MR1Ports.Add(AddPort("portMR1In", 0.01, 0.5));
            MR1Ports.Add(AddPort("portMR1Out", 1, 0.5));
            List<PointPort> MW1Ports = new List<PointPort>();
            MW1Ports.Add(AddPort("portMW1In", 0.01, 0.5));
            MW1Ports.Add(AddPort("portMW1Out", 1, 0.5));
            List<PointPort> PCS1Ports = new List<PointPort>();
            PCS1Ports.Add(AddPort("portPCS1In", 0.01, 0.5));
            PCS1Ports.Add(AddPort("portPCS1Out", 1, 0.5));

            // Memory Ports
            List<PointPort> dataMemPorts = new List<PointPort>();
            dataMemPorts.Add(AddPort("portDataMemIn0", 0.01, 0.25));
            dataMemPorts.Add(AddPort("portDataMemIn1", 0.01, 0.75));
            dataMemPorts.Add(AddPort("portDataMemIn2", 0.25, 0.01));
            dataMemPorts.Add(AddPort("portDataMemIn3", 0.75, 0.01));
            dataMemPorts.Add(AddPort("portDataMemOut", 1, 0.25));
            List<PointPort> ChkFlgPorts = new List<PointPort>();
            ChkFlgPorts.Add(AddPort("portChkFlgIn0", 0.15, 0.01));
            ChkFlgPorts.Add(AddPort("portChkFlgIn1", 0.85, 0.01));
            ChkFlgPorts.Add(AddPort("portChkFlgOut0", 0.5, 1));

            List<PointPort> memwbPorts = new List<PointPort>();
            memwbPorts.Add(AddPort("portMemwbIn0", 0.01, 0.39));
            memwbPorts.Add(AddPort("portMemwbIn1", 0.01, 0.80));
            memwbPorts.Add(AddPort("portMemwbIn2", 0.01, 0.87));
            memwbPorts.Add(AddPort("portMemwbOut0", 1, 0.39));
            memwbPorts.Add(AddPort("portMemwbOut1", 1, 0.80));
            memwbPorts.Add(AddPort("portMemwbOut2", 1, 0.87));
            List<PointPort> RW2Ports = new List<PointPort>();
            RW2Ports.Add(AddPort("portRW2In", 0.01, 0.5));
            RW2Ports.Add(AddPort("portRW2Out", 1, 0.5));
            List<PointPort> MTR2Ports = new List<PointPort>();
            MTR2Ports.Add(AddPort("portMTR2In", 0.01, 0.5));
            MTR2Ports.Add(AddPort("portMTR2Out", 1, 0.5));

            List<PointPort> FLRetPorts = new List<PointPort>();
            FLRetPorts.Add(AddPort("portFLRetIn", 1, 0.5));
            FLRetPorts.Add(AddPort("portFLRetOut", 0.01, 0.5));

            // Write Ports
            List<PointPort> WriteMuxPorts = new List<PointPort>();
            WriteMuxPorts.Add(AddPort("portWriteMuxIn0", 0.15, 0.01));
            WriteMuxPorts.Add(AddPort("portWriteMuxIn1", 0.85, 0.01));
            WriteMuxPorts.Add(AddPort("portWriteMuxIn2", 1, 0.5));
            WriteMuxPorts.Add(AddPort("portWriteMuxOut0", 0.5, 1));

            List<PointPort> RdRegReturnPorts = new List<PointPort>();
            RdRegReturnPorts.Add(AddPort("portRdRetIn", 1, 0.5));
            RdRegReturnPorts.Add(AddPort("portRdRetOut", 0.01, 0.5));

            List<PointPort> RWRetPorts = new List<PointPort>();
            RWRetPorts.Add(AddPort("portRWRetIn", 1, 0.5));
            RWRetPorts.Add(AddPort("portRWRetOut", 0.01, 0.5));

            // Window Sizing Ports
            List<PointPort> WinSizePorts = new List<PointPort>();
            #endregion

            string blueColor = "#0875F5";

            #region Nodes
            // Fetch Nodes
            CreateNode("FetchMux", 60, 293, 60, 27, -90, 90, FetchMuxPorts, FlowShapeType.Terminator, "Mux", "white", "black");
            CreateNode("PC", 110, 293, 25, 55, 0, 0, PCPorts, FlowShapeType.Process, "PC", "white", "black");
            CreateNode("InstrMem", 200, 370, 100, 100, 0, 0, InstrMemPorts, FlowShapeType.Process, "Instruction Memory", "white", "black");
            CreateNode("AddPC", 205, 250, 75, 70, -90, 90, AddPCPorts, BasicShapeType.Trapezoid, "Add", "white", "black");

            CreateNode("IFID", 300, 343, 30, 450, 0, -90, ifidPorts, FlowShapeType.Process, "IF/ID", "white", "black");

            // Decode Nodes
            CreateNode("Registers", 450, 350, 100, 100, 0, 0, regPorts, FlowShapeType.Process, "Registers", "white", "black");
            CreateNode("ImmGen", 470, 450, 40, 75, 0, 0, ImmGenPorts, BasicShapeType.Ellipse, "Imm Gen", "white", "black");
            CreateNode("Control", 480, 70, 45, 100, 0, 0, ControlPorts, BasicShapeType.Ellipse, "Control", "white", blueColor, blueColor);

            CreateNode("IDEX", 560, 343, 30, 450, 0, -90, idexPorts, FlowShapeType.Process, "ID/EX", "white", "black");
            CreateNode("RW", 560, 33, 35, 15, 0, 0, RWPorts, FlowShapeType.Process, "RW", "white", blueColor, blueColor);
            CreateNode("MTR", 560, 48, 35, 15, 0, 0, MTRPorts, FlowShapeType.Process, "MTR", "white", blueColor, blueColor);
            CreateNode("MR", 560, 63, 35, 15, 0, 0, MRPorts, FlowShapeType.Process, "MR", "white", blueColor, blueColor);
            CreateNode("MW", 560, 78, 35, 15, 0, 0, MWPorts, FlowShapeType.Process, "MW", "white", blueColor, blueColor);
            CreateNode("PCS", 560, 93, 35, 15, 0, 0, PCSPorts, FlowShapeType.Process, "PCS", "white", blueColor, blueColor);
            CreateNode("ALUS", 560, 108, 35, 15, 0, 0, ALUSPorts, FlowShapeType.Process, "ALUS", "white", blueColor, blueColor);

            // Execute Nodes
            CreateNode("ExecuteMux", 632, 378, 60, 27, -90, 90, ExecuteMuxPorts, FlowShapeType.Terminator, "Mux", "white", "black");
            CreateNode("ALU", 710, 281, 75, 70, -90, 90, ALUPorts, BasicShapeType.Trapezoid, "ALU", "white", "black");
            //CreateNode("ALUControl", 640, 490, 45, 75, 0, 0, ImmGenPorts, BasicShapeType.Ellipse, "ALU Control", "white", blueColor, blueColor);

            CreateNode("EXMEM", 800, 343, 30, 450, 0, -90, exmemPorts, FlowShapeType.Process, "EX/MEM", "white", "black");
            CreateNode("RW1", 800, 48, 35, 15, 0, 0, RW1Ports, FlowShapeType.Process, "RW", "white", blueColor, blueColor);
            CreateNode("MTR1", 800, 63, 35, 15, 0, 0, MTR1Ports, FlowShapeType.Process, "MTR", "white", blueColor, blueColor);
            CreateNode("MR1", 800, 78, 35, 15, 0, 0, MR1Ports, FlowShapeType.Process, "MR", "white", blueColor, blueColor);
            CreateNode("MW1", 800, 93, 35, 15, 0, 0, MW1Ports, FlowShapeType.Process, "MW", "white", blueColor, blueColor);
            CreateNode("PCS1", 800, 108, 35, 15, 0, 0, PCS1Ports, FlowShapeType.Process, "PCS", "white", blueColor, blueColor);

            // Memory Nodes
            CreateNode("DataMem", 950, 319, 100, 100, 0, 0, dataMemPorts, FlowShapeType.Process, "Data Memory", "white", "black");
            CreateNode("CheckFlags", 880, 164, 75, 65, -90, 90, ChkFlgPorts, BasicShapeType.Trapezoid, "Check Flgs", "white", "black");

            CreateNode("MEMWB", 1055, 343, 30, 450, 0, -90, memwbPorts, FlowShapeType.Process, "MEM/WB", "white", "black");
            CreateNode("RW2", 1055, 93, 35, 15, 0, 0, RW2Ports, FlowShapeType.Process, "RW", "white", blueColor, blueColor);
            CreateNode("MTR2", 1055, 108, 35, 15, 0, 0, MTR2Ports, FlowShapeType.Process, "MTR", "white", blueColor, blueColor);

            CreateNode("FlgReturn", 750, 10, 1, 1, 0, 0, FLRetPorts, FlowShapeType.Process, "", "white", "black");

            // Write Nodes
            CreateNode("WriteMux", 1125, 315, 60, 27, -90, 90, WriteMuxPorts, FlowShapeType.Terminator, "Mux", "white", "black");
            CreateNode("RdRegReturn", 750, 635, 1, 1, 0, 0, RdRegReturnPorts, FlowShapeType.Process, "", "white", "black");
            CreateNode("RWReturn", 750, 15, 1, 1, 0, 0, RWRetPorts, FlowShapeType.Process, "", "white", "black");

            // Window Sizing Node
            CreateNode("sizeNodeYX", 1170, 650, 1, 1, 0, 0, WinSizePorts, FlowShapeType.Process, "", "white", "white");
            #endregion

            #region Segments
            OrthogonalSegment segment1 = new OrthogonalSegment()
            {
                Type = DiagramSegments.Orthogonal,
                Length = 30,
                Direction = Direction.Right
            };
            OrthogonalSegment segment2 = new OrthogonalSegment()
            {
                Type = DiagramSegments.Orthogonal,
                Length = 300,
                Direction = Direction.Bottom
            };
            OrthogonalSegment segment3 = new OrthogonalSegment()
            {
                Type = DiagramSegments.Orthogonal,
                Length = 30,
                Direction = Direction.Left
            };
            OrthogonalSegment segment4 = new OrthogonalSegment()
            {
                Type = DiagramSegments.Orthogonal,
                Length = 200,
                Direction = Direction.Top
            };
            #endregion

            #region Connectors
            // Fetch Connectors
            CreateConnector(FetchMuxToPCIn, "FetchMux", "portFetchMuxOut0", "PC", "portPCIn", "black");
            CreateConnector(PCToInstrMemIn, "PC", "portPCOut", "InstrMem", "portInstrMemIn", "black");
            CreateConnector(PCToAddPC, "PC", "portPCOut", "AddPC", "portAddPCIn1", "black");
            CreateConnector(AddPCToFetchMux, "AddPC", "portAddPCOut0", "FetchMux", "portFetchMuxIn2", "black", "0", AnnotationAlignment.Center, .78);
            CreateConnector(IntrMemToIFID, "InstrMem", "portInstrMemOut", "IFID", "portIfidIn1", "black");
            CreateConnector(IntrMemToAddPC, "InstrMem", "portInstrMemOut1", "AddPC", "portAddPCIn0", "black", "Instr Size", AnnotationAlignment.Center, 0.15);

            // Decode Connectors
            CreateConnector(IFIDToRegIn0, "IFID", "portIfidOut1", "Registers", "portRegIn0", "black", "Rs1");
            CreateConnector(IFIDToRegIn1, "IFID", "portIfidOut1", "Registers", "portRegIn1", "black", "Rs2");
            CreateConnector(RegToIDEXIn1, "Registers", "portRegOut0", "IDEX", "portIdexIn1", "black", "RsD1", AnnotationAlignment.Before);
            CreateConnector(RegToIDEXIn2, "Registers", "portRegOut1", "IDEX", "portIdexIn2", "black", "RsD2", AnnotationAlignment.Before);
            CreateConnector(IFIDToImmGen, "IFID", "portIfidOut1", "ImmGen", "portImmGenIn", "black", "Imm/Addr Mode");
            CreateConnector(ImmGenToIDEX, "ImmGen", "portImmGenOut", "IDEX", "portIdexIn3", "black", "Result", AnnotationAlignment.Before);
            CreateConnector(IFIDToIDEX, "IFID", "portIfidOut1", "IDEX", "portIdexIn4", "black", "Rd");
            CreateConnector(IFIDToControl, "IFID", "portIfidOut1", "Control", "portControlIn", "black", "Control Bits", AnnotationAlignment.Center, .85);
            CreateConnector(ControlToRW, "Control", "portControlOut1", "RW", "portRWIn", "black");
            CreateConnector(ControlToMTR, "Control", "portControlOut1", "MTR", "portMTRIn", "black");
            CreateConnector(ControlToMR, "Control", "portControlOut1", "MR", "portMRIn", "black");
            CreateConnector(ControlToMW, "Control", "portControlOut1", "MW", "portMWIn", "black");
            CreateConnector(ControlToPCS, "Control", "portControlOut1", "PCS", "portPCSIn", "black");
            CreateConnector(ControlToALUS, "Control", "portControlOut1", "ALUS", "portALUSIn", "black");

            // Execute Connectors
            CreateConnector(RWToRW1, "RW", "portRWOut", "RW1", "portRW1In", "black");
            CreateConnector(MTRToMTR1, "MTR", "portMTROut", "MTR1", "portMTR1In", "black");
            CreateConnector(MRToMR1, "MR", "portMROut", "MR1", "portMR1In", "black");
            CreateConnector(MWToMW1, "MW", "portMWOut", "MW1", "portMW1In", "black");
            CreateConnector(PCSToPCS1, "PCS", "portPCSOut", "PCS1", "portPCS1In", "black");
            CreateConnector(ALUSToExecuteMux, "ALUS", "portALUSOut", "ExecuteMux", "portExecuteMuxIn2", "black");
            CreateConnector(IDEXToExecuteMux1, "IDEX", "portIdexOut2", "ExecuteMux", "portExecuteMuxIn1", "black", "0", AnnotationAlignment.Center, .7);
            CreateConnector(IDEXToEXMEM2, "IDEX", "portIdexOut2", "EXMEM", "portExmemIn2", "black", "RsD2", AnnotationAlignment.Center, .9);
            CreateConnector(IDEXToFetchMux, "IDEX", "portIdexOut3", "FetchMux", "portFetchMuxIn0", "black", "1", AnnotationAlignment.Center, .65);
            CreateConnector(IDEXToExecuteMux0, "IDEX", "portIdexOut3", "ExecuteMux", "portExecuteMuxIn0", "black", "1", AnnotationAlignment.Before, .7);
            CreateConnector(IDEXToEXMEM3, "IDEX", "portIdexOut4", "EXMEM", "portExmemIn3", "black", "Rd", AnnotationAlignment.Center, .9);
            CreateConnector(IDEXToALU, "IDEX", "portIdexOut1", "ALU", "portALUIn1", "black", "RsD1", AnnotationAlignment.Center, .5);
            CreateConnector(ExecuteMuxToALU, "ExecuteMux", "portExecuteMuxOut0", "ALU", "portALUIn0", "black");
            CreateConnector(ALUToEXMEM1, "ALU", "portALUOut0", "EXMEM", "portExmemIn1", "black", "ALUr", AnnotationAlignment.After, 0);
            CreateConnector(ALUToEXMEM0, "ALU", "portALUOut1", "EXMEM", "portExmemIn0", "black", "TMP FLGS", AnnotationAlignment.After, .74);

            // Memory Connectors
            CreateConnector(RW1ToRW2, "RW1", "portRW1Out", "RW2", "portRW2In", "black");
            CreateConnector(MTR1ToMTR2, "MTR1", "portMTR1Out", "MTR2", "portMTR2In", "black");
            CreateConnector(MR1ToDataMem, "MR1", "portMR1Out", "DataMem", "portDataMemIn3", "black");
            CreateConnector(MW1ToDataMem, "MW1", "portMW1Out", "DataMem", "portDataMemIn2", "black");
            CreateConnector(EXMEMToDataMem0, "EXMEM", "portExmemOut1", "DataMem", "portDataMemIn0", "black", "Addr");
            CreateConnector(EXMEMToDataMem1, "EXMEM", "portExmemOut2", "DataMem", "portDataMemIn1", "black", "RsD2");
            CreateConnector(EXMEMToMEMWB1, "EXMEM", "portExmemOut1", "MEMWB", "portMemwbIn1", "black", "ALUr");
            CreateConnector(EXMEMToMEMWB2, "EXMEM", "portExmemOut3", "MEMWB", "portMemwbIn2", "black", "Rd");
            CreateConnector(DataMemToMEMWB, "DataMem", "portDataMemOut", "MEMWB", "portMemwbIn0", "black", "Data");
            CreateConnector(PCS1ToCheckFlags, "PCS1", "portPCS1Out", "CheckFlags", "portChkFlgIn1", "black");
            CreateConnector(EXMEMToCheckFlags, "EXMEM", "portExmemOut0", "CheckFlags", "portChkFlgIn0", "black", "FL");
            CreateConnector(CheckFlagsToFlgRet, "CheckFlags", "portChkFlgOut0", "FlgReturn", "portFLRetIn", "black");
            CreateConnector(FlgRetToFetchMux, "FlgReturn", "portFLRetOut", "FetchMux", "portFetchMuxIn1", "black");

            // WriteBack Connectors
            CreateConnector(MTR2ToWriteMux, "MTR2", "portMTR2Out", "WriteMux", "portWriteMuxIn2", "black");
            CreateConnector(RW2ToRWRet, "RW2", "portRW2Out", "RWReturn", "portRWRetIn", "black");
            CreateConnector(RWRetToReg, "RWReturn", "portRWRetOut", "Registers", "portRegIn4", "black");
            CreateConnector(MEMWBToWriteMux1, "MEMWB", "portMemwbOut0", "WriteMux", "portWriteMuxIn1", "black", "1", AnnotationAlignment.Center, .5);
            CreateConnector(MEMWBToWriteMux2, "MEMWB", "portMemwbOut1", "WriteMux", "portWriteMuxIn0", "black", "0", AnnotationAlignment.Before, .8);
            CreateConnector(WriteMuxToReg, "WriteMux", "portWriteMuxOut0", "Registers", "portRegIn3", "black", "Rd Data", AnnotationAlignment.After, .5, segment1, segment2);
            CreateConnector(MEMWBToRdRet, "MEMWB", "portMemwbOut2", "RdRegReturn", "portRdRetIn", "black", "Rd Reg", AnnotationAlignment.Before, .5);
            CreateConnector(RdRetToReg, "RdRegReturn", "portRdRetOut", "Registers", "portRegIn2", "black", "Rd Reg", AnnotationAlignment.Before, .5);

            #endregion

            UpdateDiagram();
        }

        #region Connector Variables

        // Fetch Connectors
        public string FetchMuxToPCIn = "FetchMuxToPCIn";
        public string PCToInstrMemIn = "PCToInstrMemIn";
        public string PCToAddPC = "PCToAddPC";
        public string AddPCToFetchMux = "AddPCToFetchMux";
        public string IntrMemToIFID = "IntrMemToIFID";
        public string IntrMemToAddPC = "IntrMemToAddPC";

        // Decode Connectors
        public string IFIDToRegIn0 = "IFIDToRegIn0";
        public string IFIDToRegIn1 = "IFIDToRegIn1";
        public string RegToIDEXIn1 = "RegToIDEXIn1";
        public string RegToIDEXIn2 = "RegToIDEXIn2";
        public string IFIDToImmGen = "IFIDToImmGen";
        public string ImmGenToIDEX = "ImmGenToIDEX";
        public string IFIDToIDEX = "IFIDToIDEX";
        public string IFIDToControl = "IFIDToControl";
        public string ControlToRW = "ControlToRW";
        public string ControlToMTR = "ControlToMTR";
        public string ControlToMR = "ControlToMR";
        public string ControlToMW = "ControlToMW";
        public string ControlToPCS = "ControlToPCS";
        public string ControlToALUS = "ControlToALUS";

        // Execute Connectors
        public string RWToRW1 = "RWToRW1";
        public string MTRToMTR1 = "MTRToMTR1";
        public string MRToMR1 = "MRToMR1";
        public string MWToMW1 = "MWToMW1";
        public string PCSToPCS1 = "PCSToPCS1";
        public string ALUSToExecuteMux = "ALUSToExecuteMux";
        public string IDEXToExecuteMux1 = "IDEXToExecuteMux1";
        public string IDEXToEXMEM2 = "IDEXToEXMEM2";
        public string IDEXToFetchMux = "IDEXToFetchMux";
        public string IDEXToExecuteMux0 = "IDEXToExecuteMux0";
        public string IDEXToEXMEM3 = "IDEXToEXMEM3";
        public string IDEXToALU = "IDEXToALU";
        public string ExecuteMuxToALU = "ExecuteMuxToALU";
        public string ALUToEXMEM1 = "ALUToEXMEM1";
        public string ALUToEXMEM0 = "ALUToEXMEM0";

        // Memory Connectors
        public string RW1ToRW2 = "RW1ToRW2";
        public string MTR1ToMTR2 = "MTR1ToMTR2";
        public string MR1ToDataMem = "MR1ToDataMem";
        public string MW1ToDataMem = "MW1ToDataMem";
        public string EXMEMToDataMem0 = "EXMEMToDataMem0";
        public string EXMEMToDataMem1 = "EXMEMToDataMem1";
        public string EXMEMToMEMWB1 = "EXMEMToMEMWB1";
        public string EXMEMToMEMWB2 = "EXMEMToMEMWB2";
        public string DataMemToMEMWB = "DataMemToMEMWB";
        public string PCS1ToCheckFlags = "PCS1ToCheckFlags";
        public string EXMEMToCheckFlags = "EXMEMToCheckFlags";
        public string CheckFlagsToFlgRet = "CheckFlagsToFlgRet";
        public string FlgRetToFetchMux = "FlgRetToFetchMux";

        // WriteBack Connectors
        public string MTR2ToWriteMux = "MTR2ToWriteMux";
        public string RW2ToRWRet = "RW2ToRWRet";
        public string RWRetToReg = "RWRetToReg";
        public string MEMWBToWriteMux1 = "MEMWBToWriteMux1";
        public string MEMWBToWriteMux2 = "MEMWBToWriteMux2";
        public string WriteMuxToReg = "WriteMuxToReg";
        public string MEMWBToRdRet = "MEMWBToRdRet";
        public string RdRetToReg = "RdRetToReg";

        #endregion

        private void CreateConnector(string id, string sourceId, string sourcePortId, string targetId, string targetPortId, string strokeColor, string label = default, AnnotationAlignment align = AnnotationAlignment.Before, double offset = 1, OrthogonalSegment segment1 = null, OrthogonalSegment segment2 = null)
        {
            Connector diagramConnector = new Connector()
            {
                ID = id,
                SourceID = sourceId,
                SourcePortID = sourcePortId,
                TargetID = targetId,
                TargetPortID = targetPortId,
                Style = new ShapeStyle() { StrokeWidth = 1, StrokeColor = strokeColor },
                TargetDecorator = new DecoratorSettings()
                {
                    Style = new ShapeStyle() { StrokeColor = strokeColor, Fill = strokeColor }
                }
            };
            diagramConnector.Constraints |= ConnectorConstraints.DragSegmentThumb;
            diagramConnector.Type = DiagramSegments.Orthogonal;
            if (segment1 != null)
            {
                diagramConnector.Segments = new DiagramObjectCollection<ConnectorSegment>() { segment1, segment2 };
            }
            if (label != default(string))
            {
                var annotation = new PathAnnotation()
                {
                    Content = label,
                    Style = new TextStyle() { Fill = "transparent" },
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Alignment = align
                };
                annotation.Offset = offset;
                diagramConnector.Annotations = new DiagramObjectCollection<PathAnnotation>() { annotation };
            }

            ConnectorCollection.Add(diagramConnector);
        }

        private PointPort AddPort(string id, double x, double y)
        {
            return new PointPort()
            {
                ID = id,
                Shape = PortShapes.Circle,
                Width = 3,
                Height = 3,
                Visibility = PortVisibility.Visible,
                Offset = new DiagramPoint() { X = x, Y = y },
                Style = new ShapeStyle() { Fill = "#1916C1", StrokeColor = "#000" },
                Constraints = PortConstraints.Default | PortConstraints.Draw
            };
        }

        private void CreateNode(string id, double xOffset, double yOffset, int xSize, int ySize, int rAngleNode, int rAngleAnnotation,
            List<PointPort> ports, FlowShapeType shape, string label, string fillColor, string stroke, string? labelColor = default)
        {
            ShapeAnnotation annotation = new ShapeAnnotation()
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RotationAngle = rAngleAnnotation
            };
            annotation.Style = new TextStyle()
            {
                Color = labelColor != default ? labelColor : "black",
                Fill = "transparent"
            };
            Node diagramNode = new Node()
            {
                ID = id,
                OffsetX = xOffset,
                OffsetY = yOffset,
                Width = xSize,
                Height = ySize,
                RotationAngle = rAngleNode,
                Shape = new FlowShape() { Type = Shapes.Flow, Shape = shape },
                Style = new ShapeStyle() { Fill = fillColor, StrokeColor = stroke },
                Annotations = new DiagramObjectCollection<ShapeAnnotation>() { annotation },
                Ports = new DiagramObjectCollection<PointPort>(ports)
            };
            if (diagramNode.ID.ToString() == "Ready")
            {
                diagramNode.Height = 100;
                diagramNode.Width = 140;
            }
            NodeCollection.Add(diagramNode);
        }

        private void CreateNode(string id, double xOffset, double yOffset, int xSize, int ySize, int rAngleNode, int rAngleAnnotation,
            List<PointPort> ports, BasicShapeType shape, string label, string fillColor, string stroke, string labelColor = default)
        {
            ShapeAnnotation annotation = new ShapeAnnotation()
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RotationAngle = rAngleAnnotation
            };
            annotation.Style = new TextStyle()
            {
                Color = labelColor != default ? labelColor : "black",
                Fill = "transparent"
            };
            Node diagramNode = new Node()
            {
                ID = id,
                OffsetX = xOffset,
                OffsetY = yOffset,
                Width = xSize,
                Height = ySize,
                RotationAngle = rAngleNode,
                Shape = new BasicShape() { Type = Shapes.Basic, Shape = shape },
                Style = new ShapeStyle() { Fill = fillColor, StrokeColor = stroke },
                Annotations = new DiagramObjectCollection<ShapeAnnotation>() { annotation },
                Ports = new DiagramObjectCollection<PointPort>(ports)
            };
            if (diagramNode.ID.ToString() == "Ready")
            {
                diagramNode.Height = 100;
                diagramNode.Width = 140;
            }
            NodeCollection.Add(diagramNode);
        }

        public void OnItemClick()
        {
            ShowItem = !ShowItem;
        }

        private void DialogOpen(Object args)
        {
            this.ShowButton = true;
        }
        private void DialogClose(Object args)
        {
            this.ShowButton = false;
        }
        private void OnClicked()
        {
            this.Visibility = true;
        }

        private void errorClose(Object args)
        {
            this.errorVis = false;
        }

    }
}
