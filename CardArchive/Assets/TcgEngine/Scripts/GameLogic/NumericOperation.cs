namespace TcgEngine
{
    // Short-lived value input. It belongs to this Game's call stack, never to a shared asset.
    public readonly struct NumericOperation
    {
        public readonly NumericKind kind;
        public readonly NumericCause cause;
        public readonly int value;
        public readonly Card source;
        public readonly object target;

        public NumericOperation(NumericKind kind, NumericCause cause, int value, Card source, Card target)
            : this(kind, cause, value, source, (object)target) { }
        public NumericOperation(NumericKind kind, NumericCause cause, int value, Card source, Player target)
            : this(kind, cause, value, source, (object)target) { }
        private NumericOperation(NumericKind kind, NumericCause cause, int value, Card source, object target)
        {
            this.kind = kind; this.cause = cause; this.value = value; this.source = source; this.target = target;
        }
    }

    public readonly struct NumericValue
    {
        public readonly NumericKind kind;
        public readonly int value;
        public NumericValue(NumericKind kind, int value) { this.kind = kind; this.value = value; }
    }
}
