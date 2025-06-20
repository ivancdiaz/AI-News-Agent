namespace AI.News.Agent.Models
{
    // Wrapper for consistent success/failure handling
    public class Result<T>
    {
        public T? Value { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        public static Result<T> Ok(T value) => new Result<T> { Value = value };
        public static Result<T> Fail(string error) => new Result<T> { ErrorMessage = error };
    }
}