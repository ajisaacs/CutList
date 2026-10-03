namespace CutList.Core.Nesting.Search
{
    internal sealed class SearchBudgetExceededException : Exception
    {
    }

    /// <summary>Work counter shared by every search in one Pack call; patterns and nodes cost one step each.</summary>
    internal sealed class SearchBudget
    {
        private readonly long _limit;

        public SearchBudget(long limit) => _limit = limit;

        public long Used { get; private set; }
        public bool Exhausted { get; private set; }

        public void Charge()
        {
            if (++Used > _limit)
            {
                Exhausted = true;
                throw new SearchBudgetExceededException();
            }
        }
    }
}
