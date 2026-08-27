# RTL text shaping attribution

The shaping implementation in this directory is adapted from
[RTLTMPro](https://github.com/pnarimani/RTLTMPro), commit
`f480419bbbffed1be3c129d68cc0182afcfbcac3`.

The upstream implementation is licensed under the MIT License. The complete
license text is preserved in `LICENSE.RTLTMPro.txt`.

Nova keeps these types internal and integrates them through TMP's
`ITextPreprocessor`; the upstream TMP subclasses and editor integration are not
included.
