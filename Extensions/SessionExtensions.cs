using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CleaverBrooks1
{
    public static class SessionExtensions
    {
        public static bool GetBoolean(this ISession session, string key)
        {
            var data = session.Get(key);
            if(null != data)
            {
                return BitConverter.ToBoolean(data, 0);
            }

            return false;
        }

        public static void SetBoolean(this ISession session, string key, bool value)
        {
            session.Set(key, BitConverter.GetBytes(value));
        }
    }
}
