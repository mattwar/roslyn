using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var foo = new Foo();
            foo.PropertyChanged += Foo_PropertyChanged;
            foo.X = 5;
        }

        private static void Foo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine($"Property {e.PropertyName} changed.");
        }
    }

    [NPC]
    public class Foo
    {
        public int X { get; set; }
        public string Y { get; set; }
    }
}

