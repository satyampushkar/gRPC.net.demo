using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gRPC.Server.Authentication
{
    public interface IJWTAuthenticationManager
    {
        string Authenticate(string clientId, string secret);
    }
}
