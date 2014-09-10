@ECHO OFF

SET RAZZLE=C:\dd\VSPro_VBCS\src\Platform\MEF
SET GIT=%~dp0\

IF /I "%1"=="FI" (
	SET ORIGIN=%RAZZLE%
	SET TARGET=%GIT%
) ELSE IF /I "%1"=="RI" (
	SET ORIGIN=%GIT%
	SET TARGET=%RAZZLE%
) ELSE (
	ECHO Please specify either RI or FI as a parameter.
	ECHO "RI" will robocopy from git to Razzle.
	ECHO "FI" will robocopy from Razzle to git.
	EXIT /b 1
)

robocopy "%ORIGIN%" "%TARGET%" /MIR /xd packages testresults .git bin obj *.ide /XF *.err *.log *.wrn *.suo *.vspscc *asmmeta %~nx0 /NDL 

IF /I "%1"=="RI" (
	ECHO.
	ECHO *********************
	ECHO *
	ECHO * Execute the following command in Razzle:
	ECHO *
	ECHO *     tfpt online /r /deletes %%SDXROOT%%\platform\mef
	ECHO *
	ECHO *********************
)