using System;
using System.Collections.Generic;
using System.Text;

public static class StubExtention
{
    public static Dictionary<string, Type> GetBakedType<TValue>(this TValue value)
    {
        return new Dictionary<string, Type>();
    }
}
