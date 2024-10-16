using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vblocks
{
    public class PlayerState
    {
        public string SelectedTexture { get; set; } = ""; // Default texture
        public bool IsErasing { get; set; } = false; // Default mode is building
    }
}
