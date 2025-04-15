using System;
using System.Collections.Generic;
using DesignData.SDS2.Database;

using Model = DesignData.SDS2.Model;
using System.Xml;

namespace ImportAcadAsMembers
{
    class Selection : Message
    {
        /// <summary>
        /// Construct a selection from the body of a select message coming from SDS2
        /// </summary>
        public Selection(Job job, XmlNodeList items)
        {
            using (var read = new ReadOnlyTransaction(job))
            {
                List<MemberHandle> members = new List<MemberHandle>();
                List<MemberEndHandle> ends = new List<MemberEndHandle>();
                List<MaterialHandle> materials = new List<MaterialHandle>();
                List<BoltHandle> bolts = new List<BoltHandle>();
                List<WeldHandle> welds = new List<WeldHandle>();
                List<HoleHandle> holes = new List<HoleHandle>();
                foreach (XmlNode item in items)
                {
                    XmlElement aselem = item as XmlElement;
                    if (aselem == null)
                        continue;
                    int member, end, material, hole, bolt, weld;
                    if (!int.TryParse(aselem.GetAttribute("member"), out member))
                        continue;
                    if (!int.TryParse(aselem.GetAttribute("end"), out end))
                        continue;
                    if (!int.TryParse(aselem.GetAttribute("material"), out material))
                        continue;
                    if (!int.TryParse(aselem.GetAttribute("hole"), out hole))
                        continue;
                    if (!int.TryParse(aselem.GetAttribute("bolt"), out bolt))
                        continue;
                    if (!int.TryParse(aselem.GetAttribute("weld"), out weld))
                        continue;

                    if (end == -1 && material == -1 && bolt == -1 && weld == -1 && hole == -1)
                    {
                        members.Add(new MemberHandle(member));
                    }
                    else if (end >= 0 && material == -1 && bolt == -1 && weld == -1 && hole == -1)
                    {
                        ends.Add(new MemberEndHandle(member, (MemberEnd)end));
                    }
                    else if (end == -1 && material >= 0 && bolt == -1 && weld == -1 && hole == -1)
                    {
                        var m = Model.Member.Get(new MemberHandle(member));
                        var allmt = m.GetMaterial();
                        if (allmt.Count <= material)
                            continue;
                        var mt = m.GetMaterial()[material];
                        materials.Add(mt.Handle);
                    }
                    else if (end == -1 && material == -1 && bolt >= 0 && weld == -1 && hole == -1)
                    {
                        var m = Model.Member.Get(new MemberHandle(member));
                        var allmt = m.GetBolts();
                        if (allmt.Count <= bolt)
                            continue;
                        var blt = m.GetBolts()[bolt];
                        bolts.Add(blt.Handle);
                    }
                    else if (end == -1 && material == -1 && bolt == -1 && weld >= 0 && hole == -1)
                    {
                        var m = Model.Member.Get(new MemberHandle(member));
                        var allmt = m.GetWelds();
                        if (allmt.Count <= weld)
                            continue;
                        var wld = m.GetWelds()[weld];
                        welds.Add(wld.Handle);
                    }
                    else if(end == -1 && material >= 0 && bolt == -1 && weld == -1 && hole >=0)
                    {
                        var m = Model.Member.Get(new MemberHandle(member));
                        var allmt = m.GetMaterial();
                        if (allmt.Count <= material)
                            continue;
                        var mt = m.GetMaterial()[material];
                        var h = new HoleHandle(mt.Handle, hole);
                        holes.Add(h);
                    }
                }

                Members = members.ToArray();
                MemberEnds = ends.ToArray();
                Materials = materials.ToArray();
                Bolts = bolts.ToArray();
                Welds = welds.ToArray();
                Holes = holes.ToArray();
            }
        }
        public BoltHandle[] Bolts { get; private set; }
        public WeldHandle[] Welds { get; private set; }
        public MaterialHandle[] Materials { get; private set; }
        public MemberEndHandle[] MemberEnds { get; private set; }
        public MemberHandle[] Members { get; private set; }
        public HoleHandle[] Holes { get; private set; }

        public bool IsEmpty
        {
            get
            {
                return Bolts.Length == 0
                    && Welds.Length == 0
                    && Materials.Length == 0
                    && MemberEnds.Length == 0
                    && Members.Length == 0;
            }
        }
    }
}
