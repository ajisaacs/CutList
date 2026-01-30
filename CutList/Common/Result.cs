namespace CutList.Common
{
    /// <summary>
    /// Represents the result of an operation that can either succeed with a value or fail with an error.
    /// Implements the Result pattern for standardized error handling.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    public class Result<T>
    {
        private Result(T value, bool isSuccess, string error)
        {
            Value = value;
            IsSuccess = isSuccess;
            Error = error;
        }

        public T Value { get; }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        /// <summary>
        /// Creates a successful result with a value.
        /// </summary>
        public static Result<T> Success(T value)
        {
            return new Result<T>(value, true, null);
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result<T> Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error message cannot be empty", nameof(error));

            return new Result<T>(default(T), false, error);
        }

        /// <summary>
        /// Maps the success value to a new result type.
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            if (IsFailure)
                return Result<TNew>.Failure(Error);

            try
            {
                return Result<TNew>.Success(mapper(Value));
            }
            catch (Exception ex)
            {
                return Result<TNew>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Represents the result of an operation that can succeed or fail without a return value.
    /// </summary>
    public class Result
    {
        protected Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Success()
        {
            return new Result(true, null);
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error message cannot be empty", nameof(error));

            return new Result(false, error);
        }
    }
}
