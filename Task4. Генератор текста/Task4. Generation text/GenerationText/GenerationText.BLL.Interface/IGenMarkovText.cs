﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenerationTextMarkov.BLL.Interface
{
    public interface IGenMarkovText
    {
        List<string> GetWords(int countWords);
    }
}
