using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostItLater
{
    public class WrappedList<T> : List<T>
    {
        public event EventHandler ListChanged;

        public WrappedList() : base() {}
        public WrappedList(IEnumerable<T> ienumerable) : base(ienumerable) {}
        public WrappedList(Int32 i) : base(i) {}

        public void AddRange(List<T> items)
        {
            base.AddRange(items);
            this.ListChanged.Invoke(this, null);
        }

        public void Add(T item)
        {
            base.Add(item);
            this.ListChanged.Invoke(this, null);
        }

        public T RemoveAt(int index)
        {
            var item = this[index];
            base.RemoveAt(index);
            this.ListChanged.Invoke(this, null);
            return item;
        }
    }
}
