using Microsoft.Extensions.Options;

namespace Api_Celero.Services
{
    public class SimpleOptionsSnapshot<T> : IOptionsSnapshot<T> where T : class
    {
        private readonly T _value;

        public SimpleOptionsSnapshot(T value)
        {
            _value = value;
        }

        public T Value => _value;

        public T Get(string? name) => _value;
    }
}
