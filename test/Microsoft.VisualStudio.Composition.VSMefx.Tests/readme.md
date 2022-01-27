# vsmefx.Tests

## How Tests Work

The program reads through the tests present in the TestData folder and performs the following steps in each
of the tests present. It reads the command present in the text file and runs the command in vsmefx and the stores
the output printed out to standard output and standard error. It compares the output produced by the program versus
the expected output and prints out the differences if there are any differences between the output of the program and
the expected output.

## Adding Tests

The program makes it relatively easy to add a new test to run by adding a file of name [TestName].txt into the TestData folder.
The file should have the following format:

```
Command To Run
Expected Output for Standard Output
***
Expected Output for Standard Error
```

If you want to use the current output from the vsmefx program to create a new test, you can use the CreateSampleTest() method in the
TestRunner.cs file. Set the testName variable in the method to the name you want to set of the test file in the TestData folder
and the testCommand to the command to run to get the expected output. Ensure that you set the skipLabel constant to false before running
the method to ensure that xUnit doesn't ignore the method.

## Updating the Tests

As changes are made in the vsmefx program, tests might need to be updated to reflect these changes. It is relatively easy to update
the tests if there are updates in the main vsmefx program by changing the TestOverride constant in the TestRunner.cs file. What this
change does is rather than comparing the output from the program with the expected output, it stores the output from the program as the
expected output in the txt file associated with the test.
