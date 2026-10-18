using UnityEngine;

namespace TcgEngine
{
    public enum NumericKind { Any = 0, Damage = 1, Heal = 2 }
    public enum NumericCause
    {
        Any = 0, Attack = 1, CounterAttack = 2, Ability = 3, Status = 4,
        Fatigue = 5, LifeSteal = 6, Trample = 7, Other = 8
    }
    public enum NumericOperator { Set = 0, Add = 1, Multiply = 2, ConvertKind = 3 }

    [CreateAssetMenu(fileName = "numeric_modifier", menuName = "TcgEngine/Data Container/Numeric Modifier", order = 20)]
    public sealed class NumericModifierData : DataContainer
    {
        public NumericKind input_kind = NumericKind.Any;
        public NumericCause cause = NumericCause.Any;
        public NumericOperator operation;
        public int value;
        [Min(1)] public int divisor = 1;
        public NumericKind output_kind = NumericKind.Heal;
    }
}
