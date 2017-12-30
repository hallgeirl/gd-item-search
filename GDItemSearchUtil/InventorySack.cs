﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GDItemSearchUtil
{
    public class InventorySack
    {
        public List<InventoryItem> items = new List<InventoryItem>();
        byte tempBool;

        public void Read(GDFileReader file)
        {
            Block b = new Block();

            if (file.ReadBlockStart(b) != 0)
                throw new Exception();

            tempBool = file.ReadByte();
            items = GDArray<InventoryItem>.Read(file);

            file.ReadBlockEnd(b);
        }
    }
}
