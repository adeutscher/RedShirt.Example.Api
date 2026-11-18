# NuGet Packaging

## Carving API Projects

The main point of a NuGet package is to standardize and distribute a library. This API already partitions code off into
subprojects, so if you are using this template multiple times over as part of a larger effort it makes sense to consider
centralizing that logic across instances.

This template's main reasoning for not using separate NuGet packages is convenience, transparency, and development
convenience with one repo. That thinking doesn't necessarily hold up in an applied project that might not need to be
transparent with the public and might involve keeping multiple repos up to standard.

Pros of carving off logic:

* Direct API codebase is smaller.
* Easy to keep multiple APIs up to standard and prevents drift.

Cons of carving off logic:

* Need to also have a repository set up to publish NuGets to.
* Extra development steps of updating a library repository and publishing a NuGet.
* Potentially introducing package conflicts. I've particularly noticed this when dealing with AWSSDK projects, which
  have a secondary dependency on `AWSSDK.Core`. For example, using a NuGet with implementation shortcuts for S3 for a
  project that already used other AWS elements made partitioning more trouble than it was worth because of `AWSSDK.Core`
  upgrade conflicts.

At the end of the day, you'll need to weigh the standardization potential of packaging versus the speed of just
copy-pasting common code (especially if the common code is well-abstracted and partitioned off already).