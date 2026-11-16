# Virtual Corkboard
The following is an isolated archive of the virtual corkboard project. This project was completed for a university assignment in the required full-unit repository, but I've isolated just the project folder for the purposes of sharing as a portfolio piece. The project is for Unity 2022.3.21f1.

The commit messages are horrible and do not accurately reflect my current conventions.

## How does it work?
This project was designed for solving the least-thread non-overlapping configuration for any arbitrary amount of card-to-card links, provided that the links are physically possible to be made non-overlapping.

It leverages a number of pathfinding and asynchronous techniques to effectively brute force the solution. It allows pins that redirect thread to be placed on the corners of cards, and occupied corners have additional pin slots extruded outwards.

This is not the smartest nor most efficient approach, rather a good demonstration of my experience in 2024 writing compute shaders and using the C# Jobs system in Unity. My approach for this project was to use unit concepts and design a unique original approach leveraging asynchronous compute. I would encourage further research into smarter algorithms rather than depending on this one.
