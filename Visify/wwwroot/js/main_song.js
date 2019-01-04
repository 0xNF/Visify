"use strict";

// vars
var width = window.innerWidth;
var height = window.innerHeight;
var centerWidth = width/2;
var centerHeight = height/2;

var LinkThicknessBase = 1;
var LinkThicknessMult = 0.3;

var nodeOpacitySwitch = 0;
var textOpacitySwitch = 0;
var selectedNode = null;
var neighs = null;

let svg = null;
let g = null;
let node = null;
let label = null;
var nodeElements = null;

let curScale = 1.0;
let curX = 0;
let curY = 0;

let textElements = null;
let div = null;
let simulation = null;
var dragDrop = null;

var print = console.log;


var color = d3.scaleLinear()
    .domain([0, 5])
    .range(["hsl(152,80%,80%)", "hsl(228,30%,40%)"])
    .interpolate(d3.interpolateHcl);

var format = d3.format(",d");

var pack = data => d3.pack()
    .size([width, height])
    .padding(3)
  (d3.hierarchy(data)
    .sum(d => d.size)
    .sort((a, b) => b.value - a.value))

var zf2 =   
    d3.zoom().on("zoom", zoomfunc);

function zoomfunc() {
    print("ZFUNC zvar");
    print(d3.event.transform);
    curScale = d3.event.transform.k;
    curX = d3.event.transform.x;
    curY = d3.event.transform.y;
    svg.attr("transform", d3.event.transform);
}

// DOM functions
function CalculateLinkThickness(tracks){
    var len = tracks.length;
    var ret = LinkThicknessBase + (len * LinkThicknessMult);
    return ret;
}


function textOnOff(){
    textOpacitySwitch = parseInt($("#textWhich").val());
    if (textOpacitySwitch == 1){
        // none [all text is off]
        d3.selectAll("text").attr("opacity", 0);
    }else if(textOpacitySwitch == 2 || textOpacitySwitch == 3){
        if(selectNode !== null && neighs !== null && neighs.length > 0){
            textElements.attr('opacity', function(node){ return getTextOpacity(node, neighs)});
        } else{
            d3.selectAll("text").attr("opacity", 1);
        }
    } else{
        // default on
        d3.selectAll("text").attr("opacity", 1);
    }
}

function nodeOnOff(){
    nodeOpacitySwitch = parseInt($("#nodesWhich").val());
    if (nodeOpacitySwitch == 0){
        // all
        nodeElements.attr('opacity', 1);
        linkElements.attr('opacity', 1);
    } else{
        // every
        nodeElements.attr('opacity', function (node) { return getNodeOpacity(node, neighs) });
        linkElements.attr('opacity', function (link) { return getLinkOpacity(link, neighs) });
    }
}

function nodesOnOff() {

}

function ClearSearchText() {
    $("#searchText").val("");
}

function FindNode() {
    const t = $("#searchText").val();
    if (t.length == 0) {
        return;
    }
    let nx = 0;
    let ny = 0;
    let theNode = svg.selectAll('circle').filter(function (d) {
        if (d.label.toLocaleLowerCase().startsWith(t.toLocaleLowerCase())) {
            console.log(d.label);
            console.log(d);
            curX = d.x;
            curY = d.y;
            var zvar = d3.zoomIdentity.translate(d.y, d.x).scale(curScale);
            svg.transition().duration(750).call(zf2.transform, zvar);
            selectNode(d);
            return d;
        }
    });
}

// D3 Manip functions
function Recenter() {
    var zvar = d3.zoomIdentity.translate(centerWidth,centerHeight).scaleTo(curScale);
    svg.transition().duration(750).call(zf2.transform, zvar);
}

function ResetZoom() {
    curScale = 1.0;
    var zvar = d3.zoomIdentity;
    zvar.x = curX;
    zvar.y = curY;
    zvar.scale(1);
    print("RESET ZOOM zvar");
    print(zvar);
    print("END");
    svg.transition().duration(750).call(zf2.transform, zvar);
}

function resetThings() {
    g
        .attr("transform", "translate(" + curX + "," + curY + ")  scale(" + curScale + ")")
        .call(g.node().transform.scale, d3.zoomIdentity.scale(1));
}


function ResetMap() {
    Recenter();
    ResetZoom();
}

function selectNode(snode) {
    selectedNode = snode;
    var neighbors = getNeighbors(selectedNode);
    neighs = neighbors;

    // we modify the styles to highlight selected nodes
    nodeElements.attr('fill', function (node) { return getNodeColor(node, neighs) });
    nodeElements.attr('opacity', function (node) { return getNodeOpacity(node, neighs) });
    textElements.attr('fill', function (node) { return getTextColor(node, neighs) });
    textElements.attr('opacity', function(node){ return getTextOpacity(node, neighs)});
    linkElements.attr('stroke', function (link) { return getLinkColor(selectedNode, link) });
    linkElements.attr('opacity', function (link) { return getLinkOpacity(selectedNode, link) });

}

function getNeighbors(node) {
    var fetched = [node.id];
    var toFetch = [node.id];
    var depth = 0;
    var arr = [{id: node.id, depth: depth}];
    while(toFetch.length !== 0){
        var nid = toFetch.pop();
        depth+=1;
        arr = links.reduce(function (neighbors, link) {
            var ltid = link.target.id;
            var lsid = link.source.id;
            if (ltid === nid && fetched.indexOf(lsid) === -1) {
                neighbors.push({id: lsid, depth: depth});
                fetched.push(lsid);
                toFetch.push(lsid);
            }
             else if (lsid === nid && fetched.indexOf(ltid === -1)) {
                neighbors.push({id: ltid, depth: depth});
                fetched.push(ltid);
                toFetch.push(ltid);
            }
            return neighbors;
        },
            arr
        );
    }
    return arr;
}

function isNeighborLink(node, link) {
    return link.target.id === node.id || link.source.id === node.id;
}

function depth2color(depth){
    if(depth == 0){
        return "green";
    }
    else if(depth == 1){
        return "#008740";
    }
    else if (depth == 2){
        return "#008E85";
    }
    else if(depth == 3){
        return "#005895";
    }
    else if(depth == 4){
        return "#00139C";
    } 
    else if(depth == 5){
        return "#3700A3";
    } 
    else if(depth == 6){
        return "#8900AA";
    } else if (depth == 7){
        return "#B10080";
    } else if (depth == 8){
        return "#B8002F";
    }
    else {
        return "#5EBF00";
    }
}

function getNodeColor(node, neighbors) {
    var f = function(val){
        return val.id === node.id;
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1){
            return depth2color(filtered[0].depth);
        }
    }

    return node.level === 1 ? 'red' : 'gray';
}

function getNodeOpacity(node, neighbors) {
    if(nodeOpacitySwitch == 0){
        return 1;
    }
    var f = function(val){
        return val.id === node.id; 
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1 && nodeOpacitySwitch == 1){
            return 1;
        }
    }
    return 0;
}

function getLinkColor(node, link) {
    return isNeighborLink(node, link) ? 'green' : '#919191';
}

function getTextColor(node, neighbors) {
    return Array.isArray(neighbors) && neighbors.indexOf(node.id) > -1 ? 'green' : 'white';
}

function getTextOpacity(node, neighbors){

    if(textOpacitySwitch == 0){
        // indiscriminate all on
        return 1;
    } else if(textOpacitySwitch == 1){
        // indiscriminate all off
        return 0;
    }

    var f = function(val){
        return val.id === node.id;
    }

    if(Array.isArray(neighbors)){
        var filtered = neighbors.filter(f);
        if(filtered.length >= 1){
            if (textOpacitySwitch == 2) { 
                // only highlighted
                return 1;
            } else if(textOpacitySwitch == 3) {
                // non highlightd
                return 0;
            }
        }
    }
    return textOpacitySwitch === 2 ? 0 : textOpacitySwitch == 3 ? 1: 0;

}

function getLinkText(l) {
    return l.tracks.map((v, i, a) => v.name).join('\n\n・\n\n');
}

function getLinkOpacity(link, neighbors) {
    if(nodeOpacitySwitch == 0){
        return 1;
    }
    var ltid = link.target.id;
    var lsid = link.source.id;

    var f = function(val){
        return (val.id == ltid || val.id == lsid);
    }
    if(neighbors.filter(f).length > 0){
        return 1;
    }
    return 0;
}


function mainf2(data) {
    const root = pack(data);
    let focus = root;
    let view;
  
    svg = d3.select('body')
    .append('svg:svg')
    .attr('height', height)
    .attr('width', width)
    .attr("viewBox", `-${width / 2} -${height / 2} ${width} ${height}`)
    .style("display", "block")
    .style("margin", "0 -14px")
    .style("width", "calc(100% + 28px)")
    .style("height", "auto")
    .style("background", color(0))
    .style("cursor", "pointer")
    .call(zf2)
    .on("click", () => zoom(root));
  
    node = svg.append("g")
      .selectAll("circle")
      .data(root.descendants().slice(1))
      .enter().append("circle")
        .attr("fill", d => d.children ? color(d.depth) : "white")
        .attr("pointer-events", d => !d.children ? "none" : null)
        .on("mouseover", function() { d3.select(this).attr("stroke", "#000"); })
        .on("mouseout", function() { d3.select(this).attr("stroke", null); })
        .on("click", d => focus !== d && (zoom(d), d3.event.stopPropagation()))
        ;
  
    label = svg.append("g")
        .style("font", "10px sans-serif")
        .attr("pointer-events", "none")
        .attr("text-anchor", "middle")
      .selectAll("text")
      .data(root.descendants())
      .enter().append("text")
        .style("fill-opacity", d => d.parent === root ? 1 : 0)
        .style("display", d => d.parent === root ? "inline" : "none")
        .text((d) => {var n = d.data.name||"UNKNOWN"; var l = d.data.children?" ("+d.data.children.length+")" : "";  return n+l});
  
    zoomTo([root.x, root.y, root.r * 2]);
  
    function zoomTo(v) {
      const k = width / v[2];
  
      view = v;
  
      label.attr("transform", d => `translate(${(d.x - v[0]) * k},${(d.y - v[1]) * k})`);
      node.attr("transform", d => `translate(${(d.x - v[0]) * k},${(d.y - v[1]) * k})`);
      node.attr("r", d => d.r * k);
    }
  
    function zoom(d) {
      const focus0 = focus;
  
      focus = d;
  
      const transition = svg.transition()
          .duration(d3.event.altKey ? 7500 : 750)
          .tween("zoom", d => {
            const i = d3.interpolateZoom(view, [focus.x, focus.y, focus.r * 2]);
            return t => zoomTo(i(t));
          });
  
      label
        .filter(function(d) { return d.parent === focus || this.style.display === "inline"; })
        .transition(transition)
          .style("fill-opacity", d => d.parent === focus ? 1 : 0)
          .on("start", function(d) { if (d.parent === focus) this.style.display = "inline"; })
          .on("end", function(d) { if (d.parent !== focus) this.style.display = "none"; });
    }
  
    let n = svg.node();
    return n;
}


$.getJSON("/json/song.json", function (json) {
    mainf2(json)
    console.log(json); // this will show the info it in firebug console
});