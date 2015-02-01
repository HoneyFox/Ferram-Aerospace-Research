﻿/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using FerramAerospaceResearch.FARCollections;
using ferram4;

namespace FerramAerospaceResearch.FARPartGeometry
{
    class CrossSectionCurve
    {
        LLRedBlackTree<CrossSection> crossSectionsTree;
        private double maxStation, minStation;

        public double MaxStation { get { return maxStation; } }
        public double MinStation { get { return minStation; } }

        private List<CrossSection> crossSectionList = null;
        public List<CrossSection> CrossSections
        {
            get
            {
                if (crossSectionList == null)
                    crossSectionList = crossSectionsTree.InOrderTraversal();

                return crossSectionList;
            }
        }

        public CrossSectionCurve()
        {
            crossSectionsTree = new LLRedBlackTree<CrossSection>();
        }

        public CrossSectionCurve(CrossSectionCurve curveToCopy)
        {
            crossSectionsTree = new LLRedBlackTree<CrossSection>();
            List<CrossSection> sections = curveToCopy.crossSectionsTree.InOrderTraversal();
            for (int i = 0; i < sections.Count; i++)
                crossSectionsTree.Insert(sections[i]);
        }

        public void AddCrossSection(CrossSection section)
        {
            crossSectionsTree.Insert(section);

            if (section.station > maxStation)
                maxStation = section.station;
            else if (section.station < minStation)
                minStation = section.station;

            crossSectionList = null;
        }

        public void Clear()
        {
            crossSectionsTree.Clear();
            crossSectionList = null;
        }

        public CrossSection GetCrossSectionAtStation(double station)
        {
            CrossSection section = new CrossSection();
            section.station = station;

            CrossSection lowerSection, upperSection;
            crossSectionsTree.FindNearestData(section, out lowerSection, out upperSection);

            section.centroid.x = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.x, upperSection.centroid.x, station);
            section.centroid.y = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.y, upperSection.centroid.y, station);
            section.lengths.x = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.lengths.x, upperSection.lengths.x, station);
            section.lengths.y = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.lengths.y, upperSection.lengths.y, station);
            section.areaFraction = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.areaFraction, upperSection.areaFraction, station);

            return section;
        }

        public ConfigNode Save(string curveName)
        {
            ConfigNode node = new ConfigNode(curveName);
            List<CrossSection> crossSections = this.crossSectionsTree.InOrderTraversal();
            for(int i = 0; i < crossSections.Count; i++)
            {
                node.AddNode(crossSections[i].Save());
            }
            return node;
        }

        public void Load(ConfigNode node)
        {
            ConfigNode[] crossSections = node.GetNodes("CROSS_SECTION");
            for(int i = 0; i < crossSections.Length; i++)
            {
                CrossSection section = new CrossSection();
                section.Load(crossSections[i]);
                this.AddCrossSection(section);
            }
        }
    }
}
