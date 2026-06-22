//go:build windows

package main

import (
	"os"
	"strings"

	"golang.org/x/sys/windows"
)

func isRunningAsAdministrator() bool {
	sid, err := windows.CreateWellKnownSid(windows.WinBuiltinAdministratorsSid)
	if err != nil {
		return false
	}

	token := windows.Token(0)
	member, err := token.IsMember(sid)
	return err == nil && member
}

func relaunchAsAdministrator() error {
	exePath, err := os.Executable()
	if err != nil {
		return err
	}

	params := strings.Join(os.Args[1:], " ")
	verbPtr, err := windows.UTF16PtrFromString("runas")
	if err != nil {
		return err
	}
	exePtr, err := windows.UTF16PtrFromString(exePath)
	if err != nil {
		return err
	}
	paramsPtr, err := windows.UTF16PtrFromString(params)
	if err != nil {
		return err
	}
	dirPtr, err := windows.UTF16PtrFromString(appHomeDir())
	if err != nil {
		return err
	}

	return windows.ShellExecute(0, verbPtr, exePtr, paramsPtr, dirPtr, windows.SW_SHOWNORMAL)
}
