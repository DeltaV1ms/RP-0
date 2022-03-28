﻿using ContractConfigurator;
using System.Linq;
using static ConfigNode;

namespace RP0.Programs
{
    public abstract class ProgramRequirement
    {
        public bool IsInverted { get; set; }

        public abstract bool IsMet { get; }
    }

    public class ContractRequirement : ProgramRequirement
    {
        public string ContractName { get; set; }

        public override bool IsMet
        {
            get
            {
                bool b = ConfiguredContract.CompletedContracts.Any(c => c.contractType?.name == ContractName);
                return IsInverted ? !b : b;
            }
        }

        public ContractRequirement()
        {
        }

        public ContractRequirement(Value cnVal)
        {
            ContractName = cnVal.value;
            IsInverted = cnVal.name == "not_complete_contract";
        }
    }

    public class OtherProgramRequirement : ProgramRequirement
    {
        public string ProgramName { get; set; }

        public override bool IsMet
        {
            get
            {
                bool b = ProgramHandler.Instance.CompletedPrograms.Any(p => p.name == ProgramName);
                return IsInverted ? !b : b;
            }
        }

        public OtherProgramRequirement()
        {
        }

        public OtherProgramRequirement(Value cnVal)
        {

            ProgramName = cnVal.value;
            IsInverted = cnVal.name == "not_complete_program";
        }
    }
}
