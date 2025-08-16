# Task Completion Checklist

## When a Development Task is Completed

### 1. Build Verification
```cmd
dotnet build
```
- Ensure the project builds without errors
- Resolve any build warnings if introduced
- Verify all dependencies are properly referenced

### 2. Test Execution
```cmd
dotnet test Tests/Tests.csproj
```
- Run the full test suite
- Ensure all existing tests still pass
- Add new tests for new functionality if applicable
- Verify test coverage for critical paths

### 3. Code Quality Checks
- Review code against style conventions in .editorconfig
- Ensure proper naming conventions (interfaces with 'I', PascalCase, etc.)
- Verify dependency injection patterns are followed
- Check that async methods are properly implemented

### 4. Configuration Validation
- Verify any new configuration is properly documented
- Ensure user secrets are used for sensitive data
- Check that new services are registered in ServiceConfiguration
- Validate appsettings.json changes if applicable

### 5. Documentation Updates
- Update CLAUDE.md if architecture changes were made
- Add or update relevant comments for complex logic
- Ensure new features are documented appropriately
- Update README.md if user-facing changes were made

### 6. Integration Verification
- Test with different LLM providers if provider-related changes
- Verify game phases still work correctly
- Test any new plugin functionality
- Ensure vector store operations work if applicable

### 7. Git Best Practices
- Review changes with `git status` and `git diff`
- Commit with descriptive commit messages
- Consider if changes should be in separate commits
- Ensure no sensitive information is committed

## Critical Success Criteria
- ✅ Project builds successfully
- ✅ All tests pass
- ✅ No regressions in existing functionality
- ✅ New features work as intended
- ✅ Code follows established conventions
- ✅ Appropriate error handling is in place