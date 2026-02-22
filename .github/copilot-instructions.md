# Copilot Instructions

## Project Guidelines
- In xUnit tests: do not use Assert.True() to check if a value exists in a collection — use Assert.Contains instead. Do not use Assert.Equal() to check for collection size — use Assert.Empty (count==0), Assert.Single (count==1), or Assert.Equal(n, collection.Count) for n>1.