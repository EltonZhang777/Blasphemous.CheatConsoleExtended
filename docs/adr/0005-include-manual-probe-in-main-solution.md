# Include the manual Probe in the main solution

status: accepted

The net35 Console Control API Probe remains a separate, manual, non-xUnit project, and its project file is included in the main solution so contributors can discover and build it with the rest of the repository. This keeps its framework and source setup independent from the consolidated xUnit project while intentionally overriding issues #65/#66, which specified keeping it outside the solution.
