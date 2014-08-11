@ECHO OFF

SET RAZZLE=C:\dd\VSPro_VBCS\src\Platform\MEF
SET GIT=%~dp0\

IF "%1"=="FI" (
	SET ORIGIN=%RAZZLE%
	SET TARGET=%GIT%
) ELSE IF "%1"=="RI" (
	SET ORIGIN=%GIT%
	SET TARGET=%RAZZLE%
) ELSE (
	ECHO Please specify either RI or FI as a parameter.
	ECHO "RI" will robocopy from git to Razzle.
	ECHO "FI" will robocopy from Razzle to git.
	EXIT /b 1
)

robocopy "%ORIGIN%" "%TARGET%" /MIR /xd .git bin obj *.ide /XF *.err *.log *.wrn *.suo *.vspscc *asmmeta %~nx0 /NDL 
