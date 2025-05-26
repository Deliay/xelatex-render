FROM archlinux AS base
USER root
RUN pacman -Syu --noconfirm --needed
RUN pacman -S --noconfirm --needed texlive texlive-lang
RUN tlmgr install exam-zh
