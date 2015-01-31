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
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace ferram4
{
    public class FARCargoBayModule : FARPartModule, TweakScale.IRescalable<FARCargoBayModule>
    {
        [KSPField(guiActive = true, guiActiveEditor = true, isPersistant = false)]
        private int partsShielded = 0;


        private List<Part> FARShieldedParts = new List<Part>();
        public Bounds bayBounds;

        private bool bayOpen = true;

        private static int frameCounterCargo = 0;
        private static FARCargoBayModule BayController;

        private Animation bayAnim = null;
        private AnimationState bayAnimState = null;
        private string bayAnimationName;
        

        private bool bayAnimating = true;


        [KSPEvent]
        private void UpdateCargoParts()
        {
            if (HighLogic.LoadedSceneIsEditor && FARAeroUtil.EditorAboutToAttach(false) &&
                !FARAeroUtil.CurEditorParts.Contains(part))
                return;

            if (bayAnim == null || !bayOpen || HighLogic.LoadedSceneIsEditor)
                FindShieldedParts();
        }

        public override void Start()
        {
            base.Start();
            BayAnimationSetup();
            OnVesselPartsChange += UpdateCargoParts;
            if(HighLogic.LoadedSceneIsEditor)
                FindShieldedParts();
        }

        public override void OnEditorAttach()
        {
            base.OnEditorAttach();

            ClearShieldedParts();
        }

        private void BayAnimationSetup()
        {
            foreach (PartModule m in part.Modules)
            {
                FieldInfo field = m.GetType().GetField("animationName");

                if (field != null)
                {
                    bayAnimationName = (string)field.GetValue(m);
                    bayAnim = part.FindModelAnimators(bayAnimationName).FirstOrDefault();

                    if (bayAnim != null)
                    {
                        bayAnimState = bayAnim[bayAnimationName];
                        break;
                    }
                }
            }
        }


        private bool RaycastingFunction(Vector3 direction)
        {
            if (bayBounds.center == Vector3.zero)
                CalculateBayBounds();

            Ray ray = new Ray();

            ray.origin = part.transform.position;       //Set ray to start at center
            ray.direction = direction;

            bool hitMyself = false;

            // Make sure the raycast sphere fits into the bay
            Vector3 size = bayBounds.size;
            float radius = Mathf.Min(1f, Mathf.Min(size.x, size.y, size.z) * 0.15f);

            RaycastHit[] hits = Physics.SphereCastAll(ray, radius, 100, FARAeroUtil.RaycastMask);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider.attachedRigidbody)
                    if (h.collider.attachedRigidbody.GetComponent<Part>() == this.part)
                    {
                        hitMyself = true;
                    }
                if (hitMyself)
                    break;
            }


            return hitMyself;
        }


        private bool CheckBayClosed()
        {
            Vector3 forward = part.transform.forward, right = part.transform.right;

            int count = 8;
            float step = 2 * Mathf.PI / count;

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Mathf.Cos(i*step) * forward + Mathf.Sin(i*step) * right;

                if (!RaycastingFunction(dir))
                    return false;
            }

            return true;
        }

        public void FixedUpdate()
        {

            UpdateShieldedParts();
            
            if (this == BayController)
            {
                frameCounterCargo++;
                if (frameCounterCargo > 10)
                {
                    part.SendEvent("UpdateCargoParts");
                    frameCounterCargo = 0;
                }
            }
        }

        private void UpdateShieldedParts()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;
            if (bayAnim)
            {
                if ((bayAnim.isPlaying && bayAnimState.speed != 0) && !bayAnimating)
                {
                    ClearShieldedParts();
                    bayAnimating = true;
                }
                else if (bayAnimating && (!bayAnim.isPlaying || bayAnimState.speed == 0))
                {
                    bayAnimating = false;

                    if (bayOpen && CheckBayClosed())
                        FindShieldedParts();
                }

            }
            else if (BayController == null)
                BayController = this;


        }

        private void CalculateBayBounds()
        {
            for (int i = 0; i < PartBounds.Length; i++)
                bayBounds.Encapsulate(PartBounds[i]);
            /*Transform[] transformList = FARGeoUtil.PartModelTransformArray(part);
            for (int i = 0; i < transformList.Length; i++)
            {
                Transform t = transformList[i];
                
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if ((object)mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if ((object)m == null)
                    continue;

                var matrix = part.transform.worldToLocalMatrix * t.localToWorldMatrix;

                for (int j = 0; j < m.vertices.Length; j++)
                {
                    Vector3 v = matrix.MultiplyPoint3x4(m.vertices[j]);

                    maxBounds.x = Mathf.Max(maxBounds.x, v.x);
                    minBounds.x = Mathf.Min(minBounds.x, v.x);
                    maxBounds.y = Mathf.Max(maxBounds.y, v.y);
                    minBounds.y = Mathf.Min(minBounds.y, v.y);
                    maxBounds.z = Mathf.Max(maxBounds.z, v.z);
                    minBounds.z = Mathf.Min(minBounds.z, v.z);
                }
            }*/
            Vector3 expansionAmount = bayBounds.size;
            expansionAmount.x *= -0.02f;
            expansionAmount.z *= -0.02f;
            expansionAmount.y = 0;
            bayBounds.Expand(expansionAmount);
        }

        private void FindShieldedParts()
        {
            if (bayBounds.center == Vector3.zero)
            {
                CalculateBayBounds();
            }
            ClearShieldedParts();
            bayOpen = false;
            UpdateShipPartsList();

            float y_margin = (float)Math.Max(0.12, 0.03 * (bayBounds.size.y));

            Collider[] colliders = this.PartColliders;

            Bounds boundsWithMargin = bayBounds;
            boundsWithMargin.Expand(new Vector3(0, y_margin, 0));

            for (int i = 0; i < VesselPartList.Count; i++)
            {
                Part p = VesselPartList[i];

                if (FARShieldedParts.Contains(p)|| p == null || p == part || part.symmetryCounterparts.Contains(p))
                    continue;

                FARBaseAerodynamics b = null;
                FARBasicDragModel d = null;
                FARWingAerodynamicModel w = null;
                Vector3 relPos = -part.transform.position;
                w = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)w == null)
                {
                    d = p.GetComponent<FARBasicDragModel>();
                }
                if ((object)w == null && (object)d == null)
                    continue;
                //if (p.GetComponent<FARPayloadFairingModule>() != null)
                //    continue;
                if (w)
                {
                    b = w as FARBaseAerodynamics;
                    relPos += w.WingCentroid();
                }
                else
                {
                    b = d as FARBaseAerodynamics;
                    relPos += p.transform.TransformDirection(d.CenterOfDrag) + p.transform.position;       //No attach node shifting with this
                }

                relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);
                if (boundsWithMargin.Contains(relPos))
                {
                    if (relPos.y > bayBounds.max.y || relPos.y < bayBounds.min.y)
                    {
                        // Enforce strict y bounds for parent and stack children
                        if (p == this.part.parent ||
                            p.parent == this.part && p.attachMode == AttachModes.STACK)
                            continue;
                    }
                    Vector3 vecFromPToCargoBayCenter;
                    Vector3 origin;
                    if (w)
                        origin = w.WingCentroid();
                    else
                        origin = p.transform.position;

                    vecFromPToCargoBayCenter = part.transform.position - origin;

                    if (w)  //This accounts for wings that are clipping into the cargo bay.
                    {
                        origin -= vecFromPToCargoBayCenter.normalized * 0.1f;
                    }

                    RaycastHit[] hits = Physics.RaycastAll(origin, vecFromPToCargoBayCenter, vecFromPToCargoBayCenter.magnitude, FARAeroUtil.RaycastMask);

                    bool outsideMesh = false;

                    for (int j = 0; j < hits.Length; j++)
                    {
                        if (colliders.Contains(hits[j].collider))
                        {
                            outsideMesh = true;
                            break;
                        }
                    }
                    if (outsideMesh)
                        continue;

                    FARShieldedParts.Add(p);
                    if (b)
                    {
                        b.ActivateShielding();
                        //print("Shielded: " + p.partInfo.title);
                    }
                    for (int j = 0; j < p.symmetryCounterparts.Count; j++)
                    {
                        Part q = p.symmetryCounterparts[j];
                        if (q == null)
                            continue;
                        FARShieldedParts.Add(q);
                        b = q.GetComponent<FARBaseAerodynamics>();
                        if (b)
                        {
                            b.ActivateShielding();
                            //print("Shielded: " + p.partInfo.title);
                        }
                    }
                }
            }
            partsShielded = FARShieldedParts.Count;
        }

        private void ClearShieldedParts()
        {
            for (int i = 0; i < FARShieldedParts.Count; i++)
            {
                Part p = FARShieldedParts[i];

                if (p == null)
                    continue;
                FARBaseAerodynamics b = p.GetComponent<FARWingAerodynamicModel>() as FARBaseAerodynamics;
                if (b == null)
                    b = p.GetComponent<FARBasicDragModel>() as FARBaseAerodynamics;
                if (b == null)
                    continue;

                b.isShielded = false;
            }
            FARShieldedParts.Clear();
            bayOpen = true;
            partsShielded = 0;
        }

        //Blank save node ensures that nothing for this partmodule is saved
        public override void OnSave(ConfigNode node)
        {
        }

        public void OnRescale(TweakScale.ScalingFactor factor)
        {
            CalculateBayBounds();
        }
    }
}