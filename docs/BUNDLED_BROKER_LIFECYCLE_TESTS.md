# VSIX-Bundled Broker Lifecycle Tests

Use these manual regression cases for the broker copy that is bundled inside the Visual Studio extension. These cases do not apply to the standalone MSI broker, which is allowed to keep running without Visual Studio.

## Setup

1. Build and install the Debug VSIX into the Experimental Visual Studio hive.
2. Ensure no `devenv.exe` or `NetVsMcp.Broker.exe` process from a previous Experimental run is still active.
3. Launch Visual Studio Experimental with a solution, for example:

   ```powershell
   devenv.exe /RootSuffix Exp .\NetVsMcp.slnx
   ```

4. Identify the bundled broker process. Its command line should point under the Experimental VS extension directory and include `Broker\NetVsMcp.Broker.exe`.

## Case 1: Single Visual Studio Close Stops Bundled Broker

1. Start one Experimental Visual Studio instance.
2. Verify one VSIX-bundled `NetVsMcp.Broker.exe` process is running.
3. Close that Visual Studio instance normally.
4. Wait up to 25 seconds.
5. Verify the bundled broker process disappears.

Expected result: after the only Visual Studio session closes or disconnects, the VSIX-bundled broker exits.

## Case 2: Last Visual Studio Close Stops Bundled Broker

1. Start at least three Experimental Visual Studio instances.
2. Verify only one VSIX-bundled broker process is running.
3. Close one Visual Studio instance.
4. Verify the broker process is still running.
5. Close the remaining Visual Studio instances one by one.
6. After the last Visual Studio instance closes, wait up to 25 seconds.
7. Verify the broker process disappears.

Expected result: the bundled broker remains alive while at least one Visual Studio session remains, then exits after the last session is gone.

## Case 3: Broker Kill While Visual Studio Is Running

1. Start one Experimental Visual Studio instance.
2. Verify the VSIX-bundled broker process is running and record its process id.
3. Kill that broker process by process id.
4. Verify that exact process exits.
5. Keep Visual Studio open and wait up to 45 seconds.
6. Verify Visual Studio starts a replacement VSIX-bundled broker process.

Expected result: the killed broker process exits. Because Visual Studio is still running, the VSIX reconnect loop starts a replacement bundled broker.

## Case 4: Killing All Visual Studio Instances Stops Bundled Broker

1. Start multiple Experimental Visual Studio instances.
2. Verify one VSIX-bundled broker process is running.
3. Kill all Experimental `devenv.exe` processes by process id.
4. Wait up to 45 seconds.
5. Verify the bundled broker process disappears.

Expected result: after all Visual Studio processes are gone, the bundled broker exits even if normal VSIX cleanup did not run.

## Notes

- The bundled broker self-exit behavior is gated by the VSIX marker file beside the broker executable.
- The broker should not self-exit before it has seen at least one Visual Studio session.
- Standalone broker runs should continue showing standalone controls such as start at login and Exit, and should not use these bundled-broker exit expectations.
