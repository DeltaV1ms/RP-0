﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using static ConfigNode;

namespace RP0.Programs
{
    public class Program : IConfigNode
    {
        [Persistent]
        public string name;

        [Persistent(isPersistant = false)]    // Loaded from cfg but not persisted into the sfs
        public string title;

        [Persistent(isPersistant = false)]
        public string description;

        [Persistent(isPersistant = false)]
        public string requirementsPrettyText;

        [Persistent(isPersistant = false)]
        public string objectivesPrettyText;

        [Persistent(isPersistant = false)]
        public double nominalDurationYears;

        /// <summary>
        /// The amount of funds that will be paid out over the nominal duration of the program.
        /// Doesn't factor in the funds multiplier.
        /// </summary>
        [Persistent(isPersistant = false)]
        public double baseFunding;

        [Persistent]
        public double acceptedUT;

        [Persistent]
        public double completedUT;

        [Persistent]
        public double lastPaymentUT;

        /// <summary>
        /// The amount of funds that will be paid out over the nominal duration of the program.
        /// Also factors in the funds multiplier and will not change after the program has been accepted.
        /// </summary>
        [Persistent]
        public double totalFunding;

        [Persistent]
        public double fundsPaidOut;

        private Func<bool> _requirementsPredicate;
        private Func<bool> _objectivesPredicate;

        public double TotalFunding => totalFunding > 0 ? totalFunding : baseFunding * HighLogic.CurrentGame.Parameters.Career.FundsGainMultiplier;

        public Program()
        {
        }

        public Program(ConfigNode n) : this()
        {
            Load(n);
        }

        public Program(Program toCopy) : this()
        {
            name = toCopy.name;
            title = toCopy.title;
            description = toCopy.description;
            requirementsPrettyText = toCopy.requirementsPrettyText;
            objectivesPrettyText = toCopy.objectivesPrettyText;
            nominalDurationYears = toCopy.nominalDurationYears;
            baseFunding = toCopy.baseFunding;
            _requirementsPredicate = toCopy._requirementsPredicate;
            _objectivesPredicate = toCopy._objectivesPredicate;
        }

        public bool AllRequirementsMet => _requirementsPredicate == null || _requirementsPredicate();

        public bool AllObjectivesMet => _objectivesPredicate == null || _objectivesPredicate();

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);

            ConfigNode cn = node.GetNode("REQUIREMENTS");
            if (cn != null)
            {
                Expression<Func<bool>> expr = ParseRequirementBlock(cn);
                _requirementsPredicate = expr?.Compile();
            }

            cn = node.GetNode("OBJECTIVES");
            if (cn != null)
            {
                Expression<Func<bool>> expr = ParseRequirementBlock(cn);
                _objectivesPredicate = expr?.Compile();
            }
        }

        public void Save(ConfigNode node)
        {
            CreateConfigFromObject(this, node);
        }

        public Program Accept()
        {
            return new Program(this)
            {
                acceptedUT = KSPUtils.GetUT(),
                lastPaymentUT = KSPUtils.GetUT(),
                totalFunding = TotalFunding,
                fundsPaidOut = 0
            };
        }

        public void ProcessFunding()
        {
            if (TotalFunding < 1) return;

            double nowUT = KSPUtils.GetUT();
            double time2 = nowUT - acceptedUT;
            double funds2 = GetFundsAtTime(time2);
            double fundsToAdd = funds2 - fundsPaidOut;
            lastPaymentUT = nowUT;

            Debug.Log($"[RP-0] Adding {fundsToAdd} funds for program {name}");
            fundsPaidOut += fundsToAdd;
            Funding.Instance.AddFunds(fundsToAdd, TransactionReasons.Strategies);
        }

        public void Complete()
        {
            completedUT = KSPUtils.GetUT();
        }

        private double GetFundsAtTime(double time)
        {
            const double secsPerYear = 3600 * 24 * 365.25;
            double fractionOfTotalDuration = time / (nominalDurationYears * secsPerYear);
            double curveFactor = ProgramHandler.Settings.paymentCurve.Evaluate((float)fractionOfTotalDuration);
            return curveFactor * TotalFunding;
        }

        private Expression<Func<bool>> ParseRequirementBlock(ConfigNode cn)
        {
            List<Expression<Func<bool>>> expressions = ParseRequirementsAsExpressions(cn);

            foreach (ConfigNode innerCn in cn.nodes)
            {
                Expression<Func<bool>> expression = ParseRequirementBlock(innerCn);
                if (expression != null)
                {
                    expressions ??= new List<Expression<Func<bool>>>();
                    expressions.Add(expression);
                }
            }

            if (expressions == null || expressions.Count == 0) return null;

            if (cn.name.Equals("or", StringComparison.OrdinalIgnoreCase))
            {
                return expressions.CombineExpressionsWithOr();
            }
            else
            {
                return expressions.CombineExpressionsWithAnd();
            }
        }

        private List<Expression<Func<bool>>> ParseRequirementsAsExpressions(ConfigNode cn)
        {
            if (cn == null || cn.values.Count == 0) return null;

            List<Expression<Func<bool>>> expressions = new List<Expression<Func<bool>>>();
            foreach (Value cnVal in cn.values)
            {
                Expression<Func<bool>> expr = ParseRequirementAsExpression(cnVal);
                if (expr != null)
                {
                    expressions.Add(expr);
                }
            }

            return expressions;
        }

        private Expression<Func<bool>> ParseRequirementAsExpression(Value cnVal)
        {
            ProgramRequirement req = null;
            switch (cnVal.name)
            {
                case "complete_program":
                case "not_complete_program":
                    req = new OtherProgramRequirement(cnVal);
                    break;
                case "complete_contract":
                case "not_complete_contract":
                    req = new ContractRequirement(cnVal);
                    break;
                default:
                    break;
            }

            if (req == null) return null;
            else return () => req.IsMet;
        }
    }
}
