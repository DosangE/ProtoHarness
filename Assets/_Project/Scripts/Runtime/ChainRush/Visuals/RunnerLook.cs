using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: dresses the race's runner model (RunnerTilt.Body) in a runner's look (RunnerProfile.Look,
    // Art/Models/Runner*.prefab). A look has the model's hierarchy: the same children in the same order with the same
    // names (only the root's name differs), and a MeshFilter and MeshRenderer exactly where the model has them. Only
    // each MeshFilter's mesh and each MeshRenderer's materials are copied; transforms, active states and every other
    // component stay as they are, so the simulation (root, capsule, chain origin) cannot change.
    // Siblings are paired by order, not by name: the model has two "Back circuit" children under one parent.
    public static class RunnerLook
    {
        // Checks the whole hierarchy first and throws ArgumentException on the first difference, before anything is
        // changed, so a look that does not fit leaves the model exactly as it was.
        public static void Apply(Transform model, GameObject look)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (look == null) throw new ArgumentNullException(nameof(look));
            var filters = new List<(MeshFilter, MeshFilter)>();
            var renderers = new List<(MeshRenderer, MeshRenderer)>();
            Match(model, look.transform, "", look.name, filters, renderers);
            foreach (var (to, from) in filters) to.sharedMesh = from.sharedMesh;
            foreach (var (to, from) in renderers) to.sharedMaterials = from.sharedMaterials;
        }

        // Walks both hierarchies together, collecting the pairs to copy. path names the node for the error message.
        private static void Match(Transform model, Transform look, string path, string lookName,
            List<(MeshFilter, MeshFilter)> filters, List<(MeshRenderer, MeshRenderer)> renderers)
        {
            bool modelFilter = model.TryGetComponent(out MeshFilter filter);
            bool lookFilter = look.TryGetComponent(out MeshFilter fromFilter);
            if (modelFilter != lookFilter)
                throw Mismatch(lookName, path, $"a MeshFilter on the model: {YesNo(modelFilter)}, on the look: {YesNo(lookFilter)}");
            bool modelRenderer = model.TryGetComponent(out MeshRenderer renderer);
            bool lookRenderer = look.TryGetComponent(out MeshRenderer fromRenderer);
            if (modelRenderer != lookRenderer)
                throw Mismatch(lookName, path, $"a MeshRenderer on the model: {YesNo(modelRenderer)}, on the look: {YesNo(lookRenderer)}");
            if (modelFilter) filters.Add((filter, fromFilter));
            if (modelRenderer) renderers.Add((renderer, fromRenderer));

            if (model.childCount != look.childCount)
                throw Mismatch(lookName, path, $"the model has {model.childCount} children, the look {look.childCount}");
            for (int i = 0; i < model.childCount; i++)
            {
                Transform modelChild = model.GetChild(i), lookChild = look.GetChild(i);
                string childPath = Child(path, model, i);
                if (modelChild.name != lookChild.name)
                    throw Mismatch(lookName, childPath, $"child {i} is '{modelChild.name}' in the model, '{lookChild.name}' in the look");
                Match(modelChild, lookChild, childPath, lookName, filters, renderers);
            }
        }

        // "Parent/Name", with "[n]" (1-based, in order) when the parent has more than one child of that name.
        private static string Child(string path, Transform parent, int index)
        {
            string name = parent.GetChild(index).name;
            int count = 0, place = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name != name) continue;
                count++;
                if (i <= index) place = count;
            }
            string segment = count > 1 ? $"{name}[{place}]" : name;
            return path.Length == 0 ? segment : path + "/" + segment;
        }

        private static string YesNo(bool has) => has ? "yes" : "no";

        private static ArgumentException Mismatch(string lookName, string path, string what)
            => new ArgumentException($"RunnerLook: the look '{lookName}' does not match the model at " +
                $"{(path.Length == 0 ? "the root" : "'" + path + "'")}: {what}.", "look");
    }
}
