using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JiraApi.Models
{
    public class RoleDetail
    {
        public List<Actor> Actors { get; set; }
    }

    public class Actor
    {
        public string DisplayName { get; set; }
        public string Type { get; set; }
    }
}